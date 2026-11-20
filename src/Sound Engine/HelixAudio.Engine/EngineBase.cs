using System;

namespace HelixAudio.Engine
{
    /// <summary>
    /// Abstract base class for audio engine implementations, providing common functionality and validation.
    /// </summary>
    public abstract class EngineBase
    {
        /// <summary>
        /// Gets or sets the audio sample rate in Hz.
        /// </summary>
        public int SampleRate { get; protected set; }

        /// <summary>
        /// Gets or sets the number of audio channels.
        /// </summary>
        public int Channels { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the EngineBase class.
        /// </summary>
        /// <param name="sampleRate">The audio sample rate in Hz (must be positive).</param>
        /// <param name="channels">The number of audio channels (must be positive).</param>
        protected EngineBase(int sampleRate, int channels)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("Channel count must be positive.", nameof(channels));

            SampleRate = sampleRate;
            Channels = channels;
        }

        /// <summary>
        /// Validates an audio buffer for the engine.
        /// </summary>
        /// <param name="buffer">The audio buffer to validate.</param>
        /// <exception cref="ArgumentNullException"><c>buffer</c> is null.</exception>
        /// <exception cref="ArgumentException"><c>buffer</c> is empty or has invalid length.</exception>
        protected void ValidateBuffer(float[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length == 0)
                throw new ArgumentException("Buffer cannot be empty.", nameof(buffer));
            if (buffer.Length % Channels != 0)
                throw new ArgumentException($"Buffer length must be a multiple of channel count ({Channels}).", nameof(buffer));
        }

        /// <summary>
        /// Clamps a sample value to the valid range [-1.0, 1.0].
        /// </summary>
        /// <param name="sample">The sample value to clamp.</param>
        /// <returns>The clamped sample value.</returns>
        protected float ClampSample(float sample)
        {
            return sample > 1.0f ? 1.0f : (sample < -1.0f ? -1.0f : sample);
        }

        /// <summary>
        /// Calculates the number of frames from a buffer.
        /// </summary>
        /// <param name="buffer">The audio buffer.</param>
        /// <returns>The number of frames.</returns>
        protected int FramesFromSamples(float[] buffer)
        {
            return buffer.Length / Channels;
        }

        /// <summary>
        /// Gets the number of samples per frame.
        /// </summary>
        protected int SamplesPerFrame => Channels;
    }
}