using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioForge.Backend.Wasapi;

/// <summary>
/// Captures everything Windows renders to an output endpoint (the "What You
/// Hear" loopback tap) as interleaved 32-bit float PCM. This lets the engine
/// see audio from ANY application - Spotify, browsers, games - not just the
/// engine's own playback chain.
///
/// Wraps NAudio's WasapiLoopbackCapture (MIT licensed). The captured format is
/// the device mix format, which on Windows is always IEEE float; anything else
/// is rejected up front rather than silently mis-converted.
/// </summary>
public sealed class SystemLoopbackCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private bool _disposed;

    /// <summary>Interleaved float PCM blocks. Args: buffer, frames.</summary>
    public event Action<float[], int>? AudioData;

    /// <summary>Raised when capture stops unexpectedly (device removed, etc.).</summary>
    public event EventHandler<string>? Stopped;

    public bool IsRunning => _capture is not null;

    public int SampleRate { get; private set; }
    public int Channels { get; private set; }

    /// <summary>Friendly name of the tapped endpoint, for UI display.</summary>
    public string DeviceName { get; private set; } = "";

    /// <summary>
    /// Starts loopback capture on the default render endpoint, or on
    /// <paramref name="deviceId"/> when given (NAudio MMDevice ID).
    /// </summary>
    public void Start(string? deviceId = null)
    {
        Stop();
        var capture = deviceId is null
            ? new WasapiLoopbackCapture()
            : new WasapiLoopbackCapture(FindDevice(deviceId));

        var fmt = capture.WaveFormat;
        if (fmt.Encoding != WaveFormatEncoding.IeeeFloat || fmt.BitsPerSample != 32)
        {
            capture.Dispose();
            throw new InvalidOperationException(
                $"Loopback capture requires IEEE float format, got {fmt.Encoding}/{fmt.BitsPerSample}-bit.");
        }

        SampleRate = fmt.SampleRate;
        Channels = fmt.Channels;
        DeviceName = deviceId is null ? "Default playback device" : deviceId;

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) Stopped?.Invoke(this, e.Exception.Message);
        };
        _capture = capture;
        capture.StartRecording();
    }

    public void Stop()
    {
        var c = _capture;
        _capture = null;
        if (c is null) return;
        c.DataAvailable -= OnDataAvailable;
        try { c.StopRecording(); } catch { }
        c.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || AudioData is null) return;
        var floats = new float[e.BytesRecorded / 4];
        Buffer.BlockCopy(e.Buffer, 0, floats, 0, e.BytesRecorded);
        int frames = floats.Length / Math.Max(1, Channels);
        if (frames > 0) AudioData(floats, frames);
    }

    private static MMDevice FindDevice(string id)
    {
        using var en = new MMDeviceEnumerator();
        foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            if (d.ID == id || d.FriendlyName == id) return d;
        throw new ArgumentException($"Render device not found: {id}");
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
}
