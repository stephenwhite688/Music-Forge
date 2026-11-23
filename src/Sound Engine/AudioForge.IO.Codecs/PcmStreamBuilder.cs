using AudioForge.IO;
using System;
using System.Buffers.Binary;
using System.IO;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Helper that packs decoded floating-point PCM into an <see cref="AudioStream"/>
/// backed by raw IEEE float samples in memory - lossless, no int quantization.
/// </summary>
internal static class PcmStreamBuilder
{
    /// <summary>
    /// Creates an AudioStream containing the supplied interleaved float samples
    /// stored as 32-bit IEEE float.
    /// </summary>
    public static AudioStream FromFloats(int sampleRate, int channels, ReadOnlySpan<float> samples)
    {
        var bytes = new byte[samples.Length * 4];
        for (int i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), samples[i]);

        var ms = new MemoryStream(bytes);
        return new AudioStream(ms, sampleRate, channels, 32, floatPcm: true);
    }
}
