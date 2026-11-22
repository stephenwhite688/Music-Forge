using AudioForge.SoundEngine;
using HelixAudio.Backend.Wasapi;
using System;

namespace MusicForge.UI.Services;

/// <summary>
/// "System audio" source for network speakers: captures everything Windows
/// renders (Spotify, browsers, games - any app) via WASAPI loopback, runs it
/// through the live EQ/meter chain via <see cref="SystemAudioProcessor"/>,
/// and feeds the result to the network broadcast server - the same treatment
/// engine playback gets.
///
/// Mutually exclusive with Helix playback on the network feed: while active,
/// the chain's broadcast node is disabled so the two sources never interleave
/// into one stream.
/// </summary>
public sealed class SystemAudioTapService : IDisposable
{
    public static SystemAudioTapService Instance { get; } = new SystemAudioTapService();

    private readonly SystemLoopbackCapture _capture = new();
    private SystemAudioProcessor? _processor;
    private bool _disposed;

    public bool IsActive { get; private set; }
    public string? DeviceName => IsActive ? _capture.DeviceName : null;
    public int SampleRate => _capture.SampleRate;
    public int Channels => _capture.Channels;

    /// <summary>Raised when IsActive changes (UI thread marshal needed).</summary>
    public event EventHandler? Changed;

    private SystemAudioTapService()
    {
        _capture.AudioData += OnAudio;
        _capture.Stopped += (_, msg) =>
        {
            IsActive = false;
            NetworkSpeakersService.Instance.BroadcastNode.Enabled = true;
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Starts capturing the default render endpoint to network speakers.</summary>
    public void Start()
    {
        if (IsActive) return;
        var svc = NetworkSpeakersService.Instance;
        svc.EnsureServerRunning();
        _capture.Start();
        _processor = new SystemAudioProcessor(EqService.Instance.Node)
        {
            Topology = EqService.Instance.Mode == EqFilterMode.LinearPhase
                ? EqTopology.LinearPhase : EqTopology.MinimumPhase
        };
        _processor.Prepare(_capture.SampleRate, _capture.Channels);
        svc.BroadcastNode.Enabled = false;      // system audio owns the feed
        IsActive = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (!IsActive) return;
        _capture.Stop();
        _processor?.Dispose();
        _processor = null;
        NetworkSpeakersService.Instance.BroadcastNode.Enabled = true;
        IsActive = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAudio(float[] interleaved, int frames)
    {
        if (!IsActive) return;
        var server = NetworkSpeakersService.Instance.Engine.Server;
        server.SampleRate = _capture.SampleRate;
        server.SourceChannels = _capture.Channels;
        server.ChunkFrames = frames;

        // Live EQ topology may have changed since Start - keep it in sync.
        if (_processor is not null)
        {
            var want = EqService.Instance.Mode == EqFilterMode.LinearPhase
                ? EqTopology.LinearPhase : EqTopology.MinimumPhase;
            if (_processor.Topology != want) _processor.Topology = want;
            interleaved = _processor.Process(interleaved);
        }
        server.Broadcast(interleaved, frames);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _capture.Dispose();
    }
}
