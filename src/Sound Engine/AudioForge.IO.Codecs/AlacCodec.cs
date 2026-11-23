using AudioForge.Core;
using AudioForge.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading ALAC (Apple Lossless Audio Codec) files (.alac, .caf).
/// Decoding is performed through NAudio's AudioFileReader (Media Foundation on Windows).
/// </summary>
public class AlacCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("ALAC").
    /// </summary>
    public override string Name => "ALAC";

    /// <summary>
    /// Gets the file extensions supported by this codec.
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".alac", ".caf" };

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => false;
    public override string DecoderBackend => "Media Foundation (OS decoder)";
    public override bool IsVerifiedDecoder => false;


    /// <summary>
    /// Reads an ALAC audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the ALAC audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => MediaFoundationDecoder.Decode(path, cancellationToken), cancellationToken);
}
