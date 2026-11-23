using AudioForge.IO;
using OggVorbisEncoder;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// OGG Vorbis encoder using the managed OggVorbisEncoder library.
/// Deinterleaves float PCM and feeds <see cref="ProcessingState"/> per Vorbis
/// block, flushing Ogg pages as they are produced.
/// </summary>
public static class OggVorbisFileEncoder
{
    private const int FramesPerChunk = 2048;

    /// <summary>
    /// Encodes <paramref name="source"/> to an OGG Vorbis file at
    /// <paramref name="path"/>.
    /// </summary>
    /// <param name="quality">VBR quality 0..1 (default 0.5 ≈ 160kbps stereo).</param>
    public static async Task EncodeAsync(string path, AudioStream source,
        float quality = 0.5f, CancellationToken cancellationToken = default)
    {
        int sampleRate = source.SampleRate;
        int channels = Math.Max(1, source.Channels);

        var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, quality);
        var serial = Random.Shared.Next();
        var oggStream = new OggStream(serial);
        var comments = new Comments();

        oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
        oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));

        var processingState = ProcessingState.Create(info);

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
        FlushPages(oggStream, output, false);

        var interleaved = new float[FramesPerChunk * channels];
        var perChannel = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
            perChannel[ch] = new float[FramesPerChunk];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int frames = await Task.Run(() => source.ReadFramesInto(interleaved, FramesPerChunk), cancellationToken);
            if (frames <= 0) break;

            // Deinterleave.
            for (int ch = 0; ch < channels; ch++)
            {
                var dst = perChannel[ch];
                for (int f = 0; f < frames; f++)
                    dst[f] = interleaved[f * channels + ch];
            }

            processingState.WriteData(perChannel, frames);

            while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
            {
                oggStream.PacketIn(packet);
                FlushPages(oggStream, output, false);
            }
        }

        processingState.WriteEndOfStream();
        while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
        {
            oggStream.PacketIn(packet);
            FlushPages(oggStream, output, false);
        }
        FlushPages(oggStream, output, true);
    }

    private static void FlushPages(OggStream oggStream, Stream output, bool force)
    {
        while (oggStream.PageOut(out OggPage page, force))
        {
            output.Write(page.Header, 0, page.Header.Length);
            output.Write(page.Body, 0, page.Body.Length);
        }
    }
}
