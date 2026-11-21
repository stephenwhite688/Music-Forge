using HelixAudio.Core;
using HelixAudio.IO;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HelixAudio.IO.Codecs;

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
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => false;
    public override string DecoderBackend => "NVorbis (managed)";
    public override bool IsVerifiedDecoder => true;


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
                reader.Dispose);
            return stream;
        }, cancellationToken);
}
