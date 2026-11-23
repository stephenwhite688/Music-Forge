using AudioForge.Core;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading/writing Opus audio files (.opus).
/// </summary>
public class OpusCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("Opus").
    /// </summary>
    public override string Name => "Opus";

    /// <summary>
    /// Gets the file extensions supported by this codec (".opus").
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".opus" };

    /// <summary>
    /// Gets whether this codec can read audio files.
    /// </summary>
    public override bool CanRead => false;

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// Reads an Opus audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the Opus audio file.</param>
    /// <param name="cancellationToken">A token to allow cancellation of the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override async Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Opus codec requires external library implementation");
    }
}