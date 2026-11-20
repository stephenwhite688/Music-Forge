using System.Drawing;

namespace AudioForge.Visualizer.Contracts;

/// <summary>
/// Immutable snapshot of all audio data a visualiser plugin needs for one
/// frame. Built by the host from the engine's tap nodes - plugins never
/// touch the engine directly.
/// </summary>
public sealed class VisualFrame
{
    /// <summary>Mono spectrum magnitude in dBFS (FFT bins).</summary>
    public required float[] SpectrumDb { get; init; }

    /// <summary>Hz per spectrum bin.</summary>
    public float BinWidthHz { get; init; }

    /// <summary>Recent waveform, left channel (chronological).</summary>
    public required float[] WaveformLeft { get; init; }

    /// <summary>Recent waveform, right channel (chronological).</summary>
    public required float[] WaveformRight { get; init; }

    /// <summary>Per-channel RMS levels in dBFS (post-mix).</summary>
    public required float[] RmsDb { get; init; }

    /// <summary>Per-channel peak levels in dBFS (post-mix).</summary>
    public required float[] PeakDb { get; init; }

    /// <summary>Playback sample rate.</summary>
    public int SampleRate { get; init; }

    /// <summary>Display width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Display height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Seconds since the plugin started drawing (for animation).</summary>
    public double TimeSeconds { get; init; }

    /// <summary>Monotonic frame counter.</summary>
    public long FrameIndex { get; init; }

    /// <summary>True when audio is actively playing through the chain.</summary>
    public bool HasAudio { get; init; }
}
