using AudioForge.IO;
using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// MP3 encoder via LAME (libmp3lame through NAudio.Lame). Converts the
/// engine's float PCM to PCM16 and streams it through
/// <see cref="LameMP3FileWriter"/>.
/// </summary>
public static class LameMp3Encoder
{
    private const int FramesPerWrite = 1152 * 32; // ~0.75s @48k, LAME-friendly

    /// <summary>
    /// Encodes <paramref name="source"/> to an MP3 file at <paramref name="path"/>.
    /// </summary>
    /// <param name="preset">LAME preset/quality (default <see cref="LAMEPreset.ABR_128"/>).</param>
    public static async Task EncodeAsync(string path, AudioStream source,
        LAMEPreset preset = LAMEPreset.ABR_128, CancellationToken cancellationToken = default)
    {
        int sampleRate = source.SampleRate;
        int channels = Math.Max(1, source.Channels);

        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        // LameMP3FileWriter takes the *input* PCM format.
        var pcmFormat = new WaveFormat(sampleRate, 16, channels);
        using var writer = new LameMP3FileWriter(fileStream, pcmFormat, preset, null);

        var pcm = new float[FramesPerWrite * channels];
        var pcm16 = new byte[FramesPerWrite * channels * 2];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int frames = await Task.Run(() => source.ReadFramesInto(pcm, FramesPerWrite), cancellationToken);
            if (frames <= 0) break;
            int samples = frames * channels;
            for (int i = 0; i < samples; i++)
            {
                short s = (short)(Math.Clamp(pcm[i], -1f, 1f) * 32767f);
                pcm16[i * 2] = (byte)(s & 0xFF);
                pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            writer.Write(pcm16, 0, samples * 2);
        }
        writer.Flush();
    }
}
