using AudioForge.Core;
using AudioForge.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading AAC files (.aac, .adts, .m4a audio-only).
/// Decoding is performed through NAudio's AudioFileReader (Media Foundation on Windows).
/// </summary>
public class AacCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("AAC").
    /// </summary>
    public override string Name => "AAC";

    /// <summary>
    /// Gets the file extensions supported by this codec.
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".aac", ".adts", ".m4a" };

    /// <summary>
    /// Gets whether this codec can write audio files. Only .m4a/.mp4 output
    /// is supported (AAC-LC via Media Foundation SinkWriter).
    /// </summary>
    public override bool CanWrite => true;
    public override string DecoderBackend => "Media Foundation (OS decoder)";
    public override bool IsVerifiedDecoder => false;

    /// <summary>
    /// Writes an audio stream as AAC-LC in an MPEG-4 container (.m4a/.mp4).
    /// Other extensions throw <see cref="NotSupportedException"/>.
    /// </summary>
    public override Task WriteAsync(string path, AudioStream stream, CancellationToken cancellationToken = default)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".m4a" or ".mp4" or ".aac"))
            throw new NotSupportedException($"AAC codec can only write .m4a/.mp4/.aac containers, got '{ext}'.");
        return MediaFoundationAacEncoder.EncodeAsync(path, stream, cancellationToken: cancellationToken);
    }


    /// <summary>
    /// Reads an AAC audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the AAC audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => MediaFoundationDecoder.Decode(path, cancellationToken), cancellationToken);
}
