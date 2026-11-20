namespace HelixAudio.Core;

using System;

public class PanNode : AudioNode
{
    private float _pan;

    public float Pan
    {
        get => _pan;
        set => _pan = Math.Clamp(value, -1.0f, 1.0f);
    }

    public PanNode(int sampleRate, int channels, float pan = 0.0f, string name = "Pan")
        : base(sampleRate, channels, name)
    {
        if (channels != 2) throw new ArgumentException("PanNode requires stereo (2 channels)");
        _pan = Math.Clamp(pan, -1.0f, 1.0f);
    }

    public override float[] Process(float[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        float[] output = new float[input.Length];
        float pan = _pan;

        float leftGain = (float)Math.Sqrt(0.5f * (1.0f - pan));
        float rightGain = (float)Math.Sqrt(0.5f * (1.0f + pan));

        for (int i = 0; i < input.Length; i += 2)
        {
            float sampleL = input[i] * leftGain;
            float sampleR = input[i + 1] * rightGain;

            if (sampleL > 1.0f) sampleL = 1.0f;
            if (sampleL < -1.0f) sampleL = -1.0f;
            if (sampleR > 1.0f) sampleR = 1.0f;
            if (sampleR < -1.0f) sampleR = -1.0f;

            output[i] = sampleL;
            output[i + 1] = sampleR;
        }

        return output;
    }

    public override void Reset() { }
}
