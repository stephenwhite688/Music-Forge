using AudioForge.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Provides codec management functionality for reading and writing audio files.
/// Automatically registers codecs during startup and provides methods to retrieve codecs by extension or name.
/// </summary>
public static class CodecManager
{
    private static readonly Dictionary<string, IAudioCodec> _codecs = new();
    private static readonly Dictionary<string, IAudioCodec> _extensionMap = new();

    /// <summary>
    /// Static constructor that registers the default WAV codec.
    /// </summary>
    static CodecManager()
    {
        RegisterCodec(new WavCodec());
        RegisterCodec(new Mp3Codec());
        RegisterCodec(new FlacCodec());
        RegisterCodec(new OggCodec());
        RegisterCodec(new AacCodec());
        RegisterCodec(new WmaCodec());
        RegisterCodec(new AlacCodec());
    }

    /// <summary>
    /// Registers a codec for use with the codec manager.
    /// </summary>
    /// <param name="codec">The codec to register.</param>
    public static void RegisterCodec(IAudioCodec codec)
    {
        lock (_codecs)
        {
            _codecs[codec.Name] = codec;
            foreach (var ext in codec.SupportedExtensions)
            {
                _extensionMap[ext.ToLowerInvariant()] = codec;
            }
        }
    }

    /// <summary>
    /// Gets the appropriate codec for a file based on its extension.
    /// </summary>
    /// <param name="path">The path to the audio file.</param>
    /// <returns>The codec that can handle the file's extension, or null if none found.</returns>
    public static IAudioCodec? GetCodecForExtension(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        _extensionMap.TryGetValue(ext, out var codec);
        return codec;
    }

    /// <summary>
    /// Gets a codec by its name.
    /// </summary>
    /// <param name="name">The name of the codec (e.g., "WAV", "MP3").</param>
    /// <returns>The codec with the specified name, or null if not found.</returns>
    public static IAudioCodec? GetCodec(string name)
    {
        _codecs.TryGetValue(name, out var codec);
        return codec;
    }

    /// <summary>
    /// Decodes compressed audio from a stream using Media Foundation.
    /// The stream must be seekable. Used for in-memory payloads such as
    /// HLS segments where no file path exists.
    /// </summary>
    public static AudioStream DecodeStream(Stream stream)
        => MediaFoundationDecoder.DecodeStream(stream);

    /// <summary>
    /// Reads an audio file asynchronously using the appropriate codec for its extension.
    /// </summary>
    /// <param name="path">Path to the audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    /// <exception cref="NotSupportedException">Thrown when no codec is found for the file extension.</exception>
    public static async Task<AudioStream> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var codec = GetCodecForExtension(path);
        if (codec is null)
            throw new NotSupportedException($"No codec found for file: {path}");
        
        return await codec.ReadAsync(path, cancellationToken);
    }

    /// <summary>
    /// Gets all registered codecs.
    /// </summary>
    /// <returns>An enumerable collection of all registered codecs.</returns>
    /// <summary>Forensic provenance for the codec that would decode a file.</summary>
    public readonly record struct CodecTrust(string CodecName, string Backend, bool VerifiedDecoder);

    /// <summary>Gets decode provenance for a file, or null if no codec supports it.</summary>
    public static CodecTrust? GetTrustForFile(string path)
    {
        var codec = GetCodecForExtension(path);
        return codec is null ? null : new CodecTrust(codec.Name, codec.DecoderBackend, codec.IsVerifiedDecoder);
    }
    public static IEnumerable<IAudioCodec> GetAllCodecs() => _codecs.Values;
}