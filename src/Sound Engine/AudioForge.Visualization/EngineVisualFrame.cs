namespace AudioForge.Visualization;

/// <summary>
/// Engine-side visual data snapshot. Plain data only - no UI types - so any
/// host application (AudioForge, DAW, forensics tool) can consume it and
/// render however it likes. Produced by <see cref="VisualFrameProducer"/>
/// from the live tap nodes in the audio chain.
/// </summary>
public sealed class EngineVisualFrame
{
    /// <summary>Mono magnitude spectrum in dBFS (FFT bins, -120 floor).</summary>
    public required float[] SpectrumDb { get; init; }

    /// <summary>Hz per spectrum bin.</summary>
    public float BinWidthHz { get; init; }

    /// <summary>Per-channel magnitude spectra in dBFS: [channel][bin].</summary>
    public float[][]? ChannelSpectraDb { get; init; }

    /// <summary>Recent waveform per channel, chronological (oldest first).</summary>
    public required float[][] Waveforms { get; init; }

    /// <summary>Peak level in dBFS (instantaneous block peak).</summary>
    public float PeakDb { get; init; }

    /// <summary>Peak-hold level in dBFS (slow decay).</summary>
    public float HeldPeakDb { get; init; }

    /// <summary>Smoothed RMS level in dBFS.</summary>
    public float RmsDb { get; init; }

    /// <summary>True when the last block clipped (>= 0 dBFS).</summary>
    public bool Clip { get; init; }

    /// <summary>Playback sample rate.</summary>
    public int SampleRate { get; init; }

    /// <summary>Channel count.</summary>
    public int Channels { get; init; }

    /// <summary>True once the spectrum tap has analysed at least one frame.</summary>
    public bool HasAudio { get; init; }

    /// <summary>Monotonic frame counter.</summary>
    public long FrameIndex { get; init; }
}
