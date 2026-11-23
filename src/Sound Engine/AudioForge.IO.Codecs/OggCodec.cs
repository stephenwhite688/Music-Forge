using AudioForge.Core;
using AudioForge.IO;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading OGG Vorbis files (.ogg, .oga).
/// Decoding is performed through NVorbis.
/// </summary>
public class OggCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("OGG Vorbis").
    /// </summary>
    public override string Name => "OGG Vorbis";

    /// <summary>
    /// Gets the file extensions supported by this codec.
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".ogg", ".oga" };

    /// <summary>
    /// Gets whether this codec can write audio files. OGG Vorbis encoding is
    /// via the managed OggVorbisEncoder library (VBR quality 0.5 default).
    /// </summary>
    public override bool CanWrite => true;
    public override string DecoderBackend => "NVorbis (managed)";
    public override bool IsVerifiedDecoder => true;

    /// <summary>
    /// Writes an audio stream as OGG Vorbis.
    /// </summary>
    public override Task WriteAsync(string path, AudioStream stream, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedExtension(System.IO.Path.GetExtension(path)))
            throw new NotSupportedException($"OGG codec can only write .ogg/.oga files, got '{path}'.");
        return OggVorbisFileEncoder.EncodeAsync(path, stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Reads an OGG Vorbis audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the OGG file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            // Chunked decode: VorbisReader pulls and decodes on demand.
            var reader = new NVorbis.VorbisReader(path);
            int channels = reader.Channels;
            var stream = AudioStream.CreateStreaming(
                reader.SampleRate, channels, reader.TotalSamples,
                (dest, count) =>
                {
                    int wanted = Math.Min(count * channels, dest.Length);
                    wanted -= wanted % channels;
                    int read = reader.ReadSamples(dest, 0, wanted);
                    return read / channels;
                },
                frame =>
                {
                    try
                    {
                        reader.SeekTo(TimeSpan.FromSeconds(frame / (double)reader.SampleRate), SeekOrigin.Begin);
                        return true;
                    }
                    catch { return false; }
                },
                reader.Dispose);
            return stream;
        }, cancellationToken);
}
