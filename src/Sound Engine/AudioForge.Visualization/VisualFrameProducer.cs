using AudioForge.Core;
using AudioForge.DSP;

namespace AudioForge.Visualization;

/// <summary>
/// Engine-side visual data producer. Owns the tap nodes that must sit in
/// the audio chain (spectrum, per-channel waveforms, spectrogram, level
/// meter) and assembles <see cref="EngineVisualFrame"/> snapshots on demand.
///
/// Usage: add <see cref="Nodes"/> to the playback chain once, then call
/// <see cref="Capture"/> from any thread (UI timer, analysis thread) to get
/// the current frame. No UI dependencies - the host app renders however it
/// wants.
/// </summary>
public sealed class VisualFrameProducer
{
    private readonly SpectrumTapNode _spectrum;
    private readonly WaveformTapNode _waveform;
    private readonly SpectrogramTapNode _spectrogram;
    private readonly LevelMeterNode _meter;
    private long _frameIndex;

    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Channel count.</param>
    /// <param name="fftSize">Spectrum/spectrogram FFT size (power of two).</param>
    /// <param name="waveformSeconds">Seconds of waveform history retained.</param>
    /// <param name="spectrogramSeconds">Seconds of spectrogram history retained.</param>
    public VisualFrameProducer(int sampleRate = 48000, int channels = 2,
        int fftSize = 2048, double waveformSeconds = 4.0, double spectrogramSeconds = 12.0)
    {
        _spectrum = new SpectrumTapNode(sampleRate, channels, fftSize, name: "VizSpectrum");
        _waveform = new WaveformTapNode(sampleRate, channels, waveformSeconds, name: "VizWaveform");
        _spectrogram = new SpectrogramTapNode(sampleRate, channels, fftSize,
            historySeconds: spectrogramSeconds, perChannel: true, name: "VizSpectrogram");
        _meter = new LevelMeterNode(sampleRate, channels, "VizMeter");
    }

    /// <summary>The tap nodes to insert into the playback chain, in order.</summary>
    public IReadOnlyList<IAudioNode> Nodes => new IAudioNode[]
        { _meter, _spectrum, _waveform, _spectrogram };

    /// <summary>Spectrum tap (for direct access / FFT reconfiguration).</summary>
    public SpectrumTapNode Spectrum => _spectrum;

    /// <summary>Spectrogram tap (for direct snapshot access).</summary>
    public SpectrogramTapNode Spectrogram => _spectrogram;

    /// <summary>Level meter tap.</summary>
    public LevelMeterNode Meter => _meter;

    /// <summary>
    /// Captures the current visual state. <paramref name="waveformPoints"/>
    /// controls how many waveform samples per channel are returned.
    /// Allocates on the caller's thread only - safe to call at UI rate.
    /// </summary>
    public EngineVisualFrame Capture(int waveformPoints = 1024)
    {
        int channels = Math.Max(1, _spectrum.Format?.Channels ?? 2);
        var waveforms = new float[channels][];
        for (int c = 0; c < channels; c++)
            waveforms[c] = _waveform.SnapshotChannel(c, waveformPoints);

        return new EngineVisualFrame
        {
            SpectrumDb = _spectrum.SpectrumDb,
            BinWidthHz = _spectrum.BinWidthHz,
            ChannelSpectraDb = _spectrum.ChannelSpectraDb,
            Waveforms = waveforms,
            PeakDb = _meter.PeakDb,
            HeldPeakDb = _meter.HeldPeakDb,
            RmsDb = _meter.RmsDb,
            Clip = _meter.Clip,
            SampleRate = _spectrum.Format?.SampleRate ?? _spectrum.SampleRate,
            Channels = channels,
            HasAudio = _spectrum.HasData,
            FrameIndex = ++_frameIndex,
        };
    }
}
