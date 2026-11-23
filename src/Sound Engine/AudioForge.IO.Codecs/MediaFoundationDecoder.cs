using AudioForge.IO;
using System;
using System.IO;
using System.Threading;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Decodes compressed audio through NAudio's AudioFileReader (Media Foundation
/// on Windows). Handles MP3, AAC, WMA, and ALAC containers.
///
/// Chunked decode: AudioFileReader is itself a pull-based stream emitting
/// IEEE float32 PCM, so the AudioStream reads decoded frames on demand rather
/// than buffering the entire file in memory.
/// </summary>
internal static class MediaFoundationDecoder
{
    public static AudioStream Decode(string path, CancellationToken cancellationToken)
    {
        var reader = new NAudio.Wave.AudioFileReader(path);
        var wf = reader.WaveFormat;
        int channels = wf.Channels;

        // AudioFileReader.Length is the decoded PCM byte count for
        // MediaFoundation-backed readers; derive frames from it. If a future
        // backend reports source bytes instead, playback still works - only
        // the Duration estimate would be off.
        long totalFrames = reader.Length / Math.Max(1, wf.BlockAlign);

        var scratch = new byte[256 * 1024];
        var stream = AudioStream.CreateStreaming(
            wf.SampleRate, channels, totalFrames,
            (dest, count) => ReadFloats(reader, dest, count, channels, scratch),
            frame =>
            {
                try { reader.Position = frame * wf.BlockAlign; return true; }
                catch { return false; }
            },
            reader.Dispose);
        return stream;
    }

    /// <summary>
    /// Decodes a compressed audio stream (MP3/AAC/ADTS/WMA/ALAC) from any
    /// seekable <see cref="Stream"/> - used by HLS segment decode where the
    /// payload arrives as bytes rather than a file.
    /// </summary>
    public static AudioStream DecodeStream(Stream stream)
    {
        var reader = new NAudio.Wave.AudioFileReader(stream);
        var wf = reader.WaveFormat;
        int channels = wf.Channels;
        long totalFrames = reader.Length / Math.Max(1, wf.BlockAlign);

        var scratch = new byte[256 * 1024];
        return AudioStream.CreateStreaming(
            wf.SampleRate, channels, totalFrames,
            (dest, count) => ReadFloats(reader, dest, count, channels, scratch),
            frame =>
            {
                try { reader.Position = frame * wf.BlockAlign; return true; }
                catch { return false; }
            },
            reader.Dispose);
        }

    /// <summary>Pulls up to count frames of IEEE float32 from the reader.</summary>
    private static int ReadFloats(NAudio.Wave.AudioFileReader reader, float[] dest, int count, int channels, byte[] scratch)
    {
        int maxSamples = count * channels;
        if (dest.Length < maxSamples) maxSamples = dest.Length / channels * channels;
        if (maxSamples <= 0) return 0;

        int wantedBytes = maxSamples * 4;
        // Read in scratch chunks so partial reads still produce whole frames.
        int written = 0;
        while (written < wantedBytes)
        {
            int want = Math.Min(scratch.Length, wantedBytes - written);
            int got = reader.Read(scratch, 0, want);
            if (got <= 0) break;
            int floats = got / 4;
            for (int i = 0; i < floats; i++)
                dest[written / 4 + i] = BitConverter.ToSingle(scratch, i * 4);
            written += floats * 4;
            if (got < want) break;
        }
        // Drop any trailing partial frame.
        int frames = (written / 4) / channels;
        return frames;
    }
}
