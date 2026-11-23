using System;

namespace AudioForge.Core
{
    /// <summary>
    /// Represents an audio format with sample rate, channel count, and data type information.
    /// Used to describe the format of audio buffers throughout the processing pipeline.
    /// </summary>
    public sealed class AudioFormat : IEquatable<AudioFormat>
    {
        /// <summary>
        /// Gets the sample rate in Hz (e.g., 44100, 48000).
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Gets the number of audio channels (e.g., 1 for mono, 2 for stereo).
        /// </summary>
        public int Channels { get; }

        /// <summary>
        /// Gets a value indicating whether the audio data is float-point (true) or integer (false).
        /// </summary>
        public bool IsFloat { get; }

        /// <summary>
        /// Gets the bits per sample for integer audio formats (8, 16, 24, or 32).
        /// Ignored for float-point format.
        /// </summary>
        public int BitsPerSample { get; }

        /// <summary>
        /// Initializes a new instance of the AudioFormat class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz (e.g., 44100, 48000).</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        /// <param name="isFloat">Whether the audio data is float-point or integer.</param>
        /// <param name="bitsPerSample">Bits per sample for integer formats (8, 16, 24, or 32). Ignored for float format.</param>
        public AudioFormat(int sampleRate, int channels, bool isFloat, int bitsPerSample)
        {
            SampleRate = sampleRate;
            Channels = channels;
            IsFloat = isFloat;
            BitsPerSample = bitsPerSample;
        }

        /// <summary>
        /// Determines whether two AudioFormat instances are equal.
        /// </summary>
        /// <param name="other">The other object to compare.</param>
        /// <returns>True if both objects represent the same format; otherwise, false.</returns>
        public bool Equals(AudioFormat other)
        {
            if (other is null)
                return false;

            return SampleRate == other.SampleRate &&
                   Channels == other.Channels &&
                   IsFloat == other.IsFloat &&
                   BitsPerSample == other.BitsPerSample;
        }

        /// <summary>
        /// Determines whether two objects are equal.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>True if obj is an AudioFormat and equals this instance; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as AudioFormat);
        }

        /// <summary>
        /// Returns a hash code for this AudioFormat instance.
        /// </summary>
        /// <returns>A hash code based on SampleRate, Channels, IsFloat, and BitsPerSample.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(SampleRate, Channels, IsFloat, BitsPerSample);
        }
    }
}