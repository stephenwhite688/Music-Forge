using AudioForge.Core;
using AudioForge.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading WMA (Windows Media Audio) files (.wma).
/// Decoding is performed through NAudio's AudioFileReader (Media Foundation on Windows).
/// </summary>
public class WmaCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("WMA").
    /// </summary>
    public override string Name => "WMA";

    /// <summary>
    /// Gets the file extensions supported by this codec (".wma").
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".wma" };

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => false;
    public override string DecoderBackend => "Media Foundation (OS decoder)";
    public override bool IsVerifiedDecoder => false;


    /// <summary>
    /// Reads a WMA audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the WMA audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => MediaFoundationDecoder.Decode(path, cancellationToken), cancellationToken);
}
