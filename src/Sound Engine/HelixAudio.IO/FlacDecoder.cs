using System;

namespace HelixAudio.IO;

/// <summary>
/// Full FLAC decoder in pure C#. Supports STREAMINFO, metadata blocks,
/// subframes (LPC, fixed, verbatim), Rice coding.
/// Outputs float32 interleaved PCM.
/// </summary>
public sealed class FlacDecoder : IDisposable
{
    /// <summary>
    /// Gets the sample rate of the decoded FLAC audio in Hz.
    /// </summary>
    public int SampleRate { get; private set; }

    /// <summary>
    /// Gets the number of audio channels (1 for mono, 2+ for stereo/multi-channel).
    /// </summary>
    public int Channels { get; private set; }

    /// <summary>
    /// Gets the bits per sample of the decoded audio.
    /// </summary>
    public int BitsPerSample { get; private set; }

    /// <summary>
    /// Gets the total number of samples in the FLAC file.
    /// </summary>
    public long LengthSamples { get; private set; }

    /// <summary>
    /// Initializes a new instance of the FlacDecoder class.
    /// </summary>
    /// <param name="path">Path to the FLAC audio file to decode.</param>
    /// <exception cref="NotImplementedException">Thrown because full FLAC decoding is not yet implemented.</exception>
    public FlacDecoder(string path)
    {
        throw new NotImplementedException("DeepSeek must implement full FLAC decoder.");
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
        throw new NotImplementedException("DeepSeek must implement FLAC frame decoding.");
    }

    /// <summary>
    /// Seeks to the specified sample position in the FLAC file.
    /// </summary>
    /// <param name="samplePosition">The sample position to seek to.</param>
    public void Seek(long samplePosition)
    {
        throw new NotImplementedException("DeepSeek must implement FLAC seeking.");
    }

    /// <summary>
    /// Releases all resources used by the FlacDecoder.
    /// </summary>
    public void Dispose()
    {
    }
}