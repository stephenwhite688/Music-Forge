using System;

namespace HelixAudio.IO;

/// <summary>
/// Full Ogg/Vorbis decoder in pure C#. Supports Ogg pages, Vorbis headers,
/// codebooks, floor, residue, mapping.
/// Outputs float32 interleaved PCM.
/// </summary>
public sealed class OggVorbisDecoder : IDisposable
{
    /// <summary>
    /// Gets the sample rate of the decoded Ogg/Vorbis audio in Hz.
    /// </summary>
    public int SampleRate { get; private set; }

    /// <summary>
    /// Gets the number of audio channels (1 for mono, 2+ for stereo/multi-channel).
    /// </summary>
    public int Channels { get; private set; }

    /// <summary>
    /// Gets the total number of samples in the Ogg/Vorbis file.
    /// </summary>
    public long LengthSamples { get; private set; }

    /// <summary>
    /// Initializes a new instance of the OggVorbisDecoder class.
    /// </summary>
    /// <param name="path">Path to the Ogg/Vorbis audio file to decode.</param>
    /// <exception cref="NotImplementedException">Thrown because full Ogg/Vorbis decoding is not yet implemented.</exception>
    public OggVorbisDecoder(string path)
    {
        throw new NotImplementedException("DeepSeek must implement full Ogg/Vorbis decoder.");
    }

    /// <summary>
    /// Reads audio samples from the decoder into the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read samples into.</param>
    /// <param name="offset">The offset in the buffer to start writing.</param>
    /// <param name="count">The number of samples to read.</param>
    /// <returns>The actual number of samples read.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        throw new NotImplementedException("DeepSeek must implement Vorbis frame decoding.");
    }

    /// <summary>
    /// Seeks to the specified sample position in the Ogg/Vorbis file.
    /// </summary>
    /// <param name="samplePosition">The sample position to seek to.</param>
    public void Seek(long samplePosition)
    {
        throw new NotImplementedException("DeepSeek must implement Vorbis seeking.");
    }

    /// <summary>
    /// Releases all resources used by the OggVorbisDecoder.
    /// </summary>
    public void Dispose()
    {
    }
}