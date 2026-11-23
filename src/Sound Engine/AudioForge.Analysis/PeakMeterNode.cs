using AudioForge.Core;
using System;

namespace AudioForge.Analysis;

/// <summary>
/// Audio processing node that calculates and exposes peak amplitude and RMS values for audio signals.
/// </summary>
public class PeakMeterNode : AudioNode
{
    private float _peak;
    private float _rms;

    /// <summary>
    /// Gets the current peak amplitude of the processed audio.
    /// </summary>
    public float Peak => _peak;

    /// <summary>
    /// Gets the current RMS (root mean square) value of the processed audio.
    /// </summary>
    public float Rms => _rms;

    /// <summary>
    /// Initializes a new instance of the PeakMeterNode class.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
    /// <param name="name">Optional node name. Default is "Peak Meter".</param>
    public PeakMeterNode(int sampleRate, int channels, string name = "Peak Meter")
        : base(sampleRate, channels, name)
    {
        Reset();
    }

    /// <summary>
    /// Processes the input audio buffer and calculates peak/RMS metrics.
    /// Returns the original input unchanged (pass-through behavior).
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
            sumSquares += sample * sample;
            if (absSample > peak) peak = absSample;
        }

        _peak = peak;
        _rms = (float)Math.Sqrt(sumSquares / input.Length);
    }

    /// <summary>
    /// Resets the meter to initial state (zero peak and RMS).
    /// </summary>
    public override void Reset()
    {
        _peak = 0.0f;
        _rms = 0.0f;
    }
}