using AudioForge.Core;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Interface for audio codecs that can read and/or write audio files.
/// </summary>
public interface IAudioCodec
{
    /// <summary>
    /// Gets the human-readable name of the codec (e.g., "WAV", "MP3").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the file extensions this codec supports.
    /// </summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Gets whether this codec can read audio files.
    /// </summary>
    bool CanRead { get; }

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    bool CanWrite { get; }

    /// <summary>Decoder backend description for forensic provenance (e.g. "PureFlac", "Media Foundation (OS)").</summary>
    string DecoderBackend => "unspecified";

    /// <summary>True when the decode path is a deterministic, verified implementation suitable for evidentiary use.</summary>
    bool IsVerifiedDecoder => false;

    /// <summary>
    /// Reads an audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Writes an audio stream asynchronously to a file.
    /// </summary>
    /// <param name="path">Path to save the audio file.</param>
    /// <param name="stream">The audio stream to write.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task WriteAsync(string path, AudioStream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Gets whether the codec supports a specific file extension.
    /// </summary>
    /// <param name="extension">The file extension (e.g., "mp3").</param>
    /// <returns><c>true</c> if supported, <c>false</c> otherwise.</returns>
    bool IsSupportedExtension(string extension);

    /// <summary>
    /// Helper method to read all bytes from a file asynchronously.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The raw byte array of the file.</returns>
    protected async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken: cancellationToken);
        return memoryStream.ToArray();
    }
}

/// <summary>
/// Abstract base class for audio codecs implementing IAudioCodec.
/// </summary>
public abstract class AudioCodecBase : IAudioCodec
{
    /// <summary>
    /// Gets the name of the codec (e.g., "WAV", "MP3").
    /// </summary>
    public virtual string Name => throw new NotImplementedException("Name must be implemented by derived classes.");

    /// <summary>
    /// Gets the file extensions this codec supports.
    /// </summary>
    public virtual string[] SupportedExtensions => throw new NotImplementedException("SupportedExtensions must be implemented by derived classes.");

    /// <summary>
    /// Gets whether this codec can read audio files.
    /// </summary>
    public virtual bool CanRead => true;

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    public virtual bool CanWrite => true;

    public virtual string DecoderBackend => "unspecified";
    public virtual bool IsVerifiedDecoder => false;

    /// <summary>
    /// Reads an audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public virtual Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"{nameof(Name)} must be implemented by derived classes.");

    /// <summary>
    /// Writes an audio stream asynchronously to a file.
    /// </summary>
    /// <param name="path">Path to save the audio file.</param>
    /// <param name="stream">The audio stream to write.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public virtual Task WriteAsync(string path, AudioStream stream, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"{nameof(Name)} must be implemented by derived classes.");

    /// <summary>
    /// Gets whether the codec supports a specific file extension.
    /// </summary>
    /// <param name="extension">The file extension (e.g., "mp3").</param>
    /// <returns><c>true</c> if supported, <c>false</c> otherwise.</returns>
    public virtual bool IsSupportedExtension(string extension) =>
        SupportedExtensions?.Any(e => string.Equals(extension, e, StringComparison.OrdinalIgnoreCase)) ?? false;

    /// <summary>
    /// Helper method to read all bytes from a file asynchronously.
    /// </summary>
    /// <param name="path">Path to the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The raw byte array of the file.</returns>
    protected async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken: cancellationToken);
        return memoryStream.ToArray();
    }
}