using HelixAudio.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HelixAudio.IO.Codecs;

/// <summary>
/// Decodes compressed audio through NAudio's AudioFileReader (Media Foundation
/// on Windows). Handles MP3, AAC, WMA, and ALAC containers.
/// </summary>
internal static class MediaFoundationDecoder
{
    public static AudioStream Decode(string path, CancellationToken cancellationToken)
    {
        using var reader = new NAudio.Wave.AudioFileReader(path);
        int channels = reader.WaveFormat.Channels;
        int sampleRate = reader.WaveFormat.SampleRate;

        var floats = new System.Collections.Generic.List<float>(1 << 20);
        var buffer = new float[8192];
        int read;
        while ((read = reader.Read(buffer.AsSpan(0, buffer.Length))) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            floats.AddRange(new ReadOnlySpan<float>(buffer, 0, read));
        }

        return PcmStreamBuilder.FromFloats(sampleRate, channels, floats.ToArray());
    }
}
