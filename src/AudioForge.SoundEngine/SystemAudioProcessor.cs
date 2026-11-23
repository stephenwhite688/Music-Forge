using AudioForge.Core;
using AudioForge.DSP;
using AudioForge.DSP.GraphicEQ;
using System;

namespace AudioForge.SoundEngine;

/// <summary>
/// Runs WASAPI loopback PCM through a dedicated copy of the engine's EQ/meter
/// chain, so audio from ANY application (Spotify, browser, games) gets the same
/// DSP treatment as engine playback before reaching outputs or network taps.
///
/// The EQ settings are mirrored live from a source <see cref="GraphicEqNode"/>
/// on every <see cref="Process"/> call, so host-side EQ changes apply to
/// system audio immediately. Uses its own node instances - safe to run while
/// <see cref="SoundEngine"/> is also playing (no shared DSP state).
/// </summary>
public sealed class SystemAudioProcessor : IDisposable
{
    private readonly GraphicEqNode _sourceEq;
    private readonly object _gate = new();

    private GraphicEqNode? _eq;
    private LinearPhaseEqNode? _linearEq;
    private LevelMeterNode? _meter;
    private TruePeakNode? _truePeak;
    private EqTopology _topology = EqTopology.MinimumPhase;
    private AudioFormat? _format;
    private bool _disposed;

    /// <summary>The EQ node being mirrored (live reference - read its settings anytime).</summary>
    public GraphicEqNode SourceEqualizer => _sourceEq;

    /// <summary>EQ topology for the system-audio path.</summary>
    public EqTopology Topology
    {
        get => _topology;
        set { if (_topology != value) { _topology = value; _linearEq ??= CreateLinear(); } }
    }

    /// <summary>Level meter node for the system-audio path (post-EQ).</summary>
    public LevelMeterNode Meter => _meter ??= new LevelMeterNode(48000, 2, "SysAudioMeter");
    /// <summary>True-peak node for the system-audio path.</summary>
    public TruePeakNode TruePeak => _truePeak ??= new TruePeakNode(48000, 2, "SysAudioTruePeak");

    public SystemAudioProcessor(GraphicEqNode sourceEqualizer)
        => _sourceEq = sourceEqualizer ?? throw new ArgumentNullException(nameof(sourceEqualizer));

    /// <summary>
    /// Prepares the internal chain for a capture format. Call once after the
    /// loopback source reports its WaveFormat; re-call if the format changes.
    /// </summary>
    public void Prepare(int sampleRate, int channels)
    {
        var fmt = new AudioFormat(sampleRate, channels, isFloat: true, bitsPerSample: 32);
        _format = fmt;
        EnsureNodes(fmt);
    }

    /// <summary>
    /// Processes one interleaved float block through EQ -> meter -> true-peak.
    /// Returns the processed buffer (same length as input).
    /// </summary>
    public float[] Process(float[] interleaved)
    {
        if (interleaved is null || interleaved.Length == 0) return interleaved ?? Array.Empty<float>();
        lock (_gate)
        {
            if (_format is null) return interleaved; // not prepared - pass through
            SyncEqFromSource();
            ActiveEqNode.Process(interleaved);
            _meter?.Process(interleaved);
            _truePeak?.Process(interleaved);
            return interleaved;
        }
    }

    private IAudioNode ActiveEqNode =>
        _topology == EqTopology.LinearPhase ? (_linearEq ??= CreateLinear()) : (_eq ??= CreateEq());

    private GraphicEqNode CreateEq()
    {
        var n = new GraphicEqNode(_format!.SampleRate, _format.Channels, "SysAudioEQ");
        n.Prepare(_format);
        return n;
    }

    private LinearPhaseEqNode CreateLinear()
    {
        var spec = _eq ??= CreateEq();
        var n = new LinearPhaseEqNode(spec);
        n.Prepare(_format!);
        return n;
    }

    /// <summary>Copies every EQ parameter from the live source node.</summary>
    private void SyncEqFromSource()
    {
        var dst = _eq;
        if (dst is null) return;
        int ch = Math.Max(1, Math.Min(_sourceEq.PreparedChannels, dst.PreparedChannels));
        for (int c = 0; c < ch; c++)
        {
            var gains = _sourceEq.GetBandGains(c);
            for (int b = 0; b < gains.Length && b < dst.BandCount; b++)
                dst.SetBandGain(c, b, gains[b]);
        }
        var enabled = _sourceEq.GetBandEnabled();
        for (int b = 0; b < enabled.Length && b < dst.BandCount; b++)
            dst.SetBandEnabled(b, enabled[b]);
        dst.InputGainDb = _sourceEq.InputGainDb;
        dst.OutputGainDb = _sourceEq.OutputGainDb;
        dst.Bypass = _sourceEq.Bypass;
        dst.Enabled = _sourceEq.Enabled;
        dst.BandQ = _sourceEq.BandQ;
        dst.ChannelLinked = _sourceEq.ChannelLinked;
    }

    private void EnsureNodes(AudioFormat fmt)
    {
        // EQ nodes are recreated lazily at the new format on next Process
        _eq = null; _linearEq = null;
        Meter.Prepare(fmt); Meter.Reset();
        TruePeak.Prepare(fmt); TruePeak.Reset();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate) { _eq = null; _linearEq = null; _meter = null; _truePeak = null; }
    }
}
