using AudioForge.Core;
using AudioForge.IO;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading FLAC (Free Lossless Audio Codec) files (.flac).
/// Decoding is performed through PureFlac, a pure managed streaming decoder.
/// </summary>
public class FlacCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("FLAC").
    /// </summary>
    public override string Name => "FLAC";

    /// <summary>
    /// Gets the file extensions supported by this codec (".flac").
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".flac" };

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => false;
    public override string DecoderBackend => "PureFlac (managed)";
    public override bool IsVerifiedDecoder => true;


    /// <summary>
    /// Reads a FLAC audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the FLAC audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var file = File.OpenRead(path);
            var decoder = new PureFlac.FlacStreamDecoder(file, true);

            int sampleRate = decoder.SampleRate;
            int channels = decoder.Channels;
            int bitsPerSample = decoder.BitsPerSample;
            int bytesPerSample = decoder.BytesPerSample;
            if (bytesPerSample is not (2 or 3 or 4))
            {
                decoder.Dispose();
                file.Dispose();
                throw new NotSupportedException($"Unsupported FLAC bit depth: {bitsPerSample}");
            }

            // Chunked decode: pull PCM frames from the decoder on demand.
            var byteScratch = new byte[256 * 1024];
            var stream = AudioStream.CreateStreaming(
                sampleRate, channels, decoder.StreamInfo.TotalSamples,
                (dest, count) => ReadFlacFrames(decoder, dest, count, channels, bytesPerSample, byteScratch),
                null, // PureFlac decoder is forward-only - no seek
                () => { decoder.Dispose(); file.Dispose(); });
            return stream;
        }, cancellationToken);

    /// <summary>Decodes up to count frames of FLAC PCM into interleaved floats.</summary>
    private static int ReadFlacFrames(PureFlac.FlacStreamDecoder decoder, float[] dest, int count,
        int channels, int bytesPerSample, byte[] byteScratch)
    {
        int frameBytes = channels * bytesPerSample;
        int wanted = Math.Min(count * frameBytes, byteScratch.Length / frameBytes * frameBytes);
        int got = decoder.Read(byteScratch, 0, wanted);
        if (got <= 0) return 0;

        int frames = got / frameBytes;
        int sampleCount = frames * channels;
        for (int i = 0; i < sampleCount; i++)
        {
            int offset = i * bytesPerSample;
            dest[i] = bytesPerSample switch
            {
                2 => BinaryPrimitives.ReadInt16LittleEndian(byteScratch.AsSpan(offset, 2)) / 32768f,
                3 => ReadInt24LittleEndian(byteScratch, offset) / 8388608f,
                4 => BinaryPrimitives.ReadInt32LittleEndian(byteScratch.AsSpan(offset, 4)) / 2147483648f,
                _ => 0f
            };
        }
        return frames;
    }

    private static float[] PcmBytesToFloats(byte[] pcm, int channels, int bytesPerSample)
    {
        int sampleCount = pcm.Length / bytesPerSample;
        var result = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            int offset = i * bytesPerSample;
            result[i] = bytesPerSample switch
            {
                2 => BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(offset, 2)) / 32768f,
                3 => ReadInt24LittleEndian(pcm, offset) / 8388608f,
                4 => BinaryPrimitives.ReadInt32LittleEndian(pcm.AsSpan(offset, 4)) / 2147483648f,
                _ => throw new NotSupportedException($"Unsupported sample size: {bytesPerSample} bytes")
            };
        }
        return result;
    }

    private static int ReadInt24LittleEndian(byte[] data, int offset)
    {
        int value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        return (value & 0x800000) != 0 ? value | unchecked((int)0xFF000000) : value;
    }
}
