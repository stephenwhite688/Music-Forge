using AudioForge.Core;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading/writing DSD (Direct Stream Digital) audio files (.dsf, .dff).
/// </summary>
public class DsdCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("DSD").
    /// </summary>
    public override string Name => "DSD";

    /// <summary>
    /// Gets the file extensions supported by this codec (".dsf", ".dff").
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".dsf", ".dff" };

    /// <summary>
    /// Gets whether this codec can read audio files.
    /// </summary>
    public override bool CanRead => false;

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// Reads a DSD audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the DSD audio file.</param>
    /// <param name="cancellationToken">A token to allow cancellation of the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override async Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("DSD codec requires external library implementation");
    }
}