namespace HelixAudio.Core;

using System;

public class SilenceSource : AudioSourceBase
{
    public SilenceSource(int sampleRate, int channels, string name = "Silence")
        : base(sampleRate, channels, name)
    {
    }

    public override float[] Read(int frameCount)
    {
        if (frameCount <= 0) return Array.Empty<float>();
        return new float[frameCount * Channels];
    }

    public override void Reset() { }
}
