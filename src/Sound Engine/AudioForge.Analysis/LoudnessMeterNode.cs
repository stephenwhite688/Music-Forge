using AudioForge.Core;
using System;

namespace AudioForge.Analysis;

/// <summary>
/// Audio processing node that calculates and exposes loudness and peak values for audio signals.
/// </summary>
public class LoudnessMeterNode : AudioNode
{
    private float _loudness;
    private float _peak;

    /// <summary>
    /// Gets the current loudness value of the processed audio.
    /// </summary>
    public float Loudness => _loudness;

    /// <summary>
    /// Gets the peak amplitude of the processed audio.
    /// </summary>
    public float Peak => _peak;

    /// <summary>
    /// Initializes a new instance of the LoudnessMeterNode class.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
    /// <param name="name">Optional node name. Default is "Loudness Meter".</param>
    public LoudnessMeterNode(int sampleRate, int channels, string name = "Loudness Meter")
        : base(sampleRate, channels, name)
    {
        Reset();
    }

    /// <summary>
    /// Processes the input audio buffer in place and calculates loudness/peak metrics.
    /// Pass-through: the buffer is not modified.
    /// </summary>
    /// <param name="input">The interleaved audio buffer to analyze.</param>
    public override void Process(Span<float> input)
    {
        double sumSquares = 0.0;
        float peak = 0.0f;

        for (int i = 0; i < input.Length; i++)
        {
            float sample = input[i];
            float absSample = Math.Abs(sample);

            float weighted = absSample < 0.1f ? absSample * 0.5f : absSample * 1.5f;
            sumSquares += weighted * weighted;

            if (absSample > peak) peak = absSample;
        }

        _loudness = (float)Math.Sqrt(sumSquares / input.Length);
        _peak = peak;
    }

    /// <summary>
    /// Resets the meter to initial state (zero loudness and peak).
    /// </summary>
    public override void Reset()
    {
        _loudness = 0.0f;
        _peak = 0.0f;
    }
}