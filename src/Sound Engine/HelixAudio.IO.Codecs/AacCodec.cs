using HelixAudio.Core;
using HelixAudio.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HelixAudio.IO.Codecs;

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
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => false;
    public override string DecoderBackend => "Media Foundation (OS decoder)";
    public override bool IsVerifiedDecoder => false;


    /// <summary>
    /// Reads an AAC audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the AAC audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => MediaFoundationDecoder.Decode(path, cancellationToken), cancellationToken);
}
