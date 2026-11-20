using System;

namespace HelixAudio.Core;

/// <summary>
/// Audio gain node that scales the amplitude of audio input.
/// </summary>
public class GainNode : AudioNode
{
    private float _gain;

    /// <summary>
    /// Gets or sets the gain value (0.0 to infinity). Negative values are clamped to 0.
    /// </summary>
    public float Gain
    {
        get => _gain;
        set => _gain = Math.Max(0.0f, value);
    }

    /// <summary>
    /// Initializes a new instance of the GainNode class.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate in Hz.</param>
    /// <param name="channels">The number of audio channels.</param>
    /// <param name="gain">The initial gain value (default: 1.0).</param>
    /// <param name="name">The node name (default: "Gain").</param>
    public GainNode(int sampleRate, int channels, float gain = 1.0f, string name = "Gain")
        : base(sampleRate, channels, name)
    {
        _gain = Math.Max(0.0f, gain);
    }

    /// <inheritdoc/>
    public override float[] Process(float[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        float[] output = new float[input.Length];
        float gain = _gain;

        for (int i = 0; i < input.Length; i++)
        {
            float sample = input[i] * gain;
            if (sample > 1.0f) sample = 1.0f;
            if (sample < -1.0f) sample = -1.0f;
            output[i] = sample;
        }

        return output;
    }

    /// <inheritdoc/>
    public override void Reset() { }
}