using System;

namespace HelixAudio.IO;

/// <summary>
/// Full MP3 decoder using pure C#. Supports MPEG1/2 Layer III.
/// Outputs float32 interleaved PCM.
/// </summary>
public sealed class Mp3Decoder : IDisposable
{
    /// <summary>
    /// Gets the sample rate of the decoded MP3 audio in Hz.
    /// </summary>
    public int SampleRate { get; private set; }

    /// <summary>
    /// Gets the number of audio channels (1 for mono, 2+ for stereo).
    /// </summary>
    public int Channels { get; private set; }

    /// <summary>
    /// Gets the total number of samples in the MP3 file.
    /// </summary>
    public long LengthSamples { get; private set; }

    /// <summary>
    /// Initializes a new instance of the Mp3Decoder class.
    /// </summary>
    /// <param name="path">Path to the MP3 audio file to decode.</param>
    /// <exception cref="NotImplementedException">Thrown because full MP3 decoding is not yet implemented.</exception>
    public Mp3Decoder(string path)
    {
        throw new NotImplementedException("DeepSeek must implement full MP3 decoder.");
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
        throw new NotImplementedException("DeepSeek must implement MP3 frame decoding.");
    }

    /// <summary>
    /// Seeks to the specified sample position in the MP3 file.
    /// </summary>
    /// <param name="samplePosition">The sample position to seek to.</param>
    public void Seek(long samplePosition)
    {
        throw new NotImplementedException("DeepSeek must implement MP3 seeking.");
    }

    /// <summary>
    /// Releases all resources used by the Mp3Decoder.
    /// </summary>
    public void Dispose()
    {
    }
}