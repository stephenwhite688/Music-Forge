using AudioForge.SoundEngine;
using AudioForge.Backend.Wasapi;
using AudioForge.Core;
using AudioForge.DSP;
using AudioForge.DSP.GraphicEQ;
using AudioForge.Engine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.SoundEngine;

/// <summary>EQ filter topology.</summary>
public enum EqTopology { MinimumPhase, LinearPhase }

/// <summary>
/// AudioForge standalone sound engine. Owns realtime playback (files and
/// streams), the default DSP chain (meters, graphic EQ, true-peak), the
/// WASAPI output device, and a system-audio loopback capture.
///
/// Host applications plug in extra processing by appending their own
/// <see cref="IAudioNode"/> instances to <see cref="OutputTaps"/> - e.g. the
/// AudioForge.NetworkSpeakers package's PcmBroadcastNode - which receive the
/// fully processed PCM at the end of the chain. The engine never references
/// consumers; taps are plain nodes.
/// </summary>
public sealed class SoundEngine : IDisposable
{
    private readonly RealtimePlayer _player = new();
    private readonly object _gate = new();

    private readonly GraphicEqNode _eq = new(48000, 2, "MasterEQ");
    private LinearPhaseEqNode? _linearEq;
    private EqTopology _eqTopology = EqTopology.MinimumPhase;

    private readonly LevelMeterNode _inputMeter = new(48000, 2, "InputMeter");
    private readonly SpectrumTapNode _preEq = new(48000, 2, name: "PreEqSpectrum");
    private readonly SpectrumTapNode _postEq = new(48000, 2, name: "PostEqSpectrum");
    private readonly LevelMeterNode _outputMeter = new(48000, 2, "OutputMeter");
    private readonly TruePeakNode _truePeak = new(48000, 2, "TruePeak");

    private readonly List<IAudioNode> _outputTaps = new();
    private readonly SystemLoopbackCapture _loopback = new();
    private readonly Timer _positionTimer;
    private bool _disposed;

    /// <summary>Raised on the playback thread when a track/stream ends naturally.</summary>
    public event EventHandler? PlaybackCompleted;

    /// <summary>Raised on the playback thread on fatal errors.</summary>
    public event EventHandler<Exception>? PlaybackError;

    /// <summary>Raised ~4x/sec while playing or paused (poll <see cref="Position"/>).</summary>
    public event EventHandler? PositionChanged;

    public SoundEngine()
    {
        _player.PlaybackCompleted += (_, _) => PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        _player.PlaybackError += (_, ex) => PlaybackError?.Invoke(this, ex);
        _positionTimer = new Timer(_ =>
        {
            if (_player.IsPlaying || _player.IsPaused)
                PositionChanged?.Invoke(this, EventArgs.Empty);
        }, null, Timeout.Infinite, Timeout.Infinite);
        _positionTimer.Change(250, 250);
    }

    // ---------------- transport ----------------

    /// <summary>Plays an audio file through decode -> DSP chain -> output.</summary>
    public Task PlayFileAsync(string path)
        => _player.PlayAsync(path, BuildChain());

    /// <summary>Plays a live HTTP/HTTPS audio stream (radio, internet stream).</summary>
    public void PlayStream(string url) => _player.PlayStream(url, nodes: BuildChain());

    public void Pause() => _player.Pause();
    public void Resume() => _player.Resume();

    /// <summary>Play/Pause toggle.</summary>
    public void TogglePlayPause()
    {
        if (_player.IsPaused) _player.Resume();
        else if (_player.IsPlaying) _player.Pause();
    }

    public void Stop() => _player.Stop();

    public bool IsPlaying => _player.IsPlaying;
    public bool IsPaused => _player.IsPaused;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.Duration;
    public int SampleRate => _player.SampleRate;
    public int Channels => _player.Channels;

    /// <summary>Output volume 0..1 (maps to WASAPI device volume).</summary>
    public float Volume
    {
        get => _player.Volume;
        set => _player.Volume = value;
    }

    /// <summary>
    /// All active Windows render endpoints - speakers, headphones, Bluetooth,
    /// USB DACs, HDMI outputs. Use <see cref="OutputDeviceId"/> to select one.
    /// </summary>
    public static IReadOnlyList<(string Id, string Name)> OutputDevices =>
        WasapiDeviceManager.EnumerateOutputDevices();

    /// <summary>
    /// ID of the render endpoint to play through (from
    /// <see cref="OutputDevices"/>). Null/empty = Windows default device.
    /// Applies to the next Play call - changing it mid-playback takes effect
    /// on the following track.
    /// </summary>
    public string? OutputDeviceId
    {
        get => string.IsNullOrEmpty(_player.OutputDeviceId) ? null : _player.OutputDeviceId;
        set => _player.OutputDeviceId = value ?? "";
    }

    // ---------------- DSP ----------------

    /// <summary>
    /// The graphic EQ node. Configure bands directly
    /// (<see cref="GraphicEqNode.SetBandGain"/>, <see cref="GraphicEqNode.Bypass"/>,
    /// <see cref="GraphicEqNode.BandQ"/>, <see cref="GraphicEqNode.ChannelLinked"/>...);
    /// changes apply to subsequent playback immediately.
    /// </summary>
    public GraphicEqNode Equalizer => _eq;

    /// <summary>EQ topology: minimum-phase biquads (low latency) or linear-phase FIR.</summary>
    public EqTopology EqTopology
    {
        get => _eqTopology;
        set
        {
            if (_eqTopology == value) return;
            _eqTopology = value;
            _linearEq ??= new LinearPhaseEqNode(_eq);
        }
    }

    /// <summary>The EQ node currently in the chain (biquad or linear-phase wrapper).</summary>
    public IAudioNode ActiveEqNode =>
        _eqTopology == EqTopology.LinearPhase ? (_linearEq ??= new LinearPhaseEqNode(_eq)) : _eq;

    // ---------------- meters ----------------

    /// <summary>Pre-EQ level meter node - poll for peak/RMS.</summary>
    public LevelMeterNode InputMeter => _inputMeter;
    /// <summary>Post-chain level meter node.</summary>
    public LevelMeterNode OutputMeter => _outputMeter;
    /// <summary>Pre-EQ spectrum tap for visualizers.</summary>
    public SpectrumTapNode PreEqSpectrum => _preEq;
    /// <summary>Post-EQ spectrum tap for visualizers.</summary>
    public SpectrumTapNode PostEqSpectrum => _postEq;
    /// <summary>BS.1770 true-peak meter node.</summary>
    public TruePeakNode TruePeak => _truePeak;

    // ---------------- consumer taps ----------------

    /// <summary>
    /// Extra nodes appended to the END of the processing chain, after EQ and
    /// meters. Use this to tap the processed PCM - e.g. add a
    /// PcmBroadcastNode (AudioForge.NetworkSpeakers) to stream to network
    /// speakers, or any custom <see cref="IAudioNode"/> of your own.
    /// Nodes are prepared/reset automatically on each Play call.
    /// </summary>
    public IList<IAudioNode> OutputTaps => _outputTaps;

    // ---------------- system audio loopback ----------------

    /// <summary>
    /// WASAPI loopback capture of the default render endpoint - everything the
    /// PC plays (any app) as interleaved float PCM via
    /// <see cref="SystemLoopbackCapture.AudioData"/>. Independent of playback;
    /// start/stop it directly.
    /// </summary>
    public SystemLoopbackCapture Loopback => _loopback;

    /// <summary>
    /// Creates a <see cref="SystemAudioProcessor"/> bound to this engine's EQ -
    /// feed it WASAPI loopback blocks and system audio gets the same DSP
    /// treatment (EQ, meters, true-peak) as engine playback.
    /// </summary>
    public SystemAudioProcessor CreateSystemAudioProcessor() =>
        new(_eq) { Topology = _eqTopology };

    // ---------------- internals ----------------

    /// <summary>Assembles the processing chain for a play call.</summary>
    private IAudioNode[] BuildChain()
    {
        var chain = new List<IAudioNode>
        {
            _inputMeter, _preEq, ActiveEqNode, _postEq, _outputMeter, _truePeak
        };
        lock (_gate) chain.AddRange(_outputTaps);
        return chain.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _positionTimer.Dispose();
        _loopback.Dispose();
        _player.Dispose();
    }
}
