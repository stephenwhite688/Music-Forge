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
            using var reader = new NVorbis.VorbisReader(path);
            int channels = reader.Channels;
            int sampleRate = reader.SampleRate;

            var floats = new List<float>(1 << 20);
            var buffer = new float[8192];
            int read;
            while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                floats.AddRange(new ReadOnlySpan<float>(buffer, 0, read));
            }

            return PcmStreamBuilder.FromFloats(sampleRate, channels, floats.ToArray());
        }, cancellationToken);
}
