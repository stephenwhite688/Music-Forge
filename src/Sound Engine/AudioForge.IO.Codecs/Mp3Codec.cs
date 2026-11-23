using AudioForge.Core;
using AudioForge.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading MP3 (MPEG-1/2 Audio Layer III) files (.mp3).
/// Decoding is performed through NAudio's AudioFileReader (Media Foundation on Windows).
/// </summary>
public class Mp3Codec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("MP3").
    /// </summary>
    public override string Name => "MP3";

    /// <summary>
    /// Gets the file extensions supported by this codec (".mp3").
    /// </summary>
    public override string[] SupportedExtensions => new[] { ".mp3" };

    /// <summary>
    /// Gets whether this codec can write audio files. MP3 encoding is via
    /// LAME (libmp3lame, LGPL) through NAudio.Lame.
    /// </summary>
    public override bool CanWrite => true;
    public override string DecoderBackend => "Media Foundation (OS decoder)";
    public override bool IsVerifiedDecoder => false;

    /// <summary>
    /// Writes an audio stream as MP3 via LAME (default 128kbps ABR).
    /// </summary>
    public override Task WriteAsync(string path, AudioStream stream, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedExtension(Path.GetExtension(path)))
            throw new NotSupportedException($"MP3 codec can only write .mp3 files, got '{path}'.");
        return LameMp3Encoder.EncodeAsync(path, stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Reads an MP3 audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the MP3 audio file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decoded audio stream.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => MediaFoundationDecoder.Decode(path, cancellationToken), cancellationToken);
}
