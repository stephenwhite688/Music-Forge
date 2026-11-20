namespace HelixAudio.DSP;
using HelixAudio.Core;
using System;

public abstract class DSPNode : AudioNode
{
    protected DSPNode(int sr, int ch, string n) : base(sr, ch, n) { }

    protected void ValidateBuffer(float[] buffer)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        if (buffer.Length == 0)
            throw new ArgumentException("Buffer cannot be empty.", nameof(buffer));

        if (buffer.Length % Channels != 0)
            throw new ArgumentException("Buffer length must be a multiple of the channel count.", nameof(buffer));
    }

    public abstract float ProcessSample(float sample);

    public override float[] Process(float[] input)
    {
        ValidateBuffer(input);

        float[] output = new float[input.Length];

        for (int i = 0; i < input.Length; i++)
            output[i] = ProcessSample(input[i]);

        return output;
    }
}
