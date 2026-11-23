using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Abstract base class for audio analysis nodes.
    /// </summary>
    public abstract class AnalysisBase
    {
        /// <summary>
        /// Gets the sample rate in Hz.
        /// </summary>
        protected int SampleRate { get; private set; }

        /// <summary>
        /// Gets the number of audio channels.
        /// </summary>
        protected int Channels { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AnalysisBase class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        protected AnalysisBase(int sampleRate, int channels)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("Channel count must be positive.", nameof(channels));

            SampleRate = sampleRate;
            Channels = channels;
        }

        /// <summary>
        /// Analyzes the input buffer and returns a single float value.
        /// </summary>
        /// <param name="input">The audio input buffer.</param>
        /// <returns>A single analysis result.</returns>
        public abstract float Analyze(float[] input);

        /// <summary>
        /// Analyzes a frame of audio data and returns frame-specific analysis values.
        /// </summary>
        /// <param name="input">The audio input buffer containing frames.</param>
        /// <returns>An array of float values representing the analysis for each channel.</returns>
        public abstract float[] AnalyzeFrame(float[] input);

        /// <summary>
        /// Validates the input buffer and throws an exception if it's null, empty, or has an invalid length.
        /// </summary>
        /// <param name="input">The input buffer to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when input is empty or not a multiple of channel count.</exception>
        protected void ValidateInput(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException("Input buffer cannot be empty.", nameof(input));
            if (input.Length % Channels != 0)
                throw new ArgumentException($"Input buffer length must be a multiple of channel count ({Channels}).", nameof(input));
        }

        /// <summary>
        /// Clamps a sample value to the range [-1.0, 1.0].
        /// </summary>
        /// <param name="sample">The sample value to clamp.</param>
        /// <returns>The clamped sample value.</returns>
        protected float ClampSample(float sample)
        {
            return sample > 1.0f ? 1.0f : (sample < -1.0f ? -1.0f : sample);
        }

        /// <summary>
        /// Converts a multi-channel audio buffer to mono by averaging all channels.
        /// </summary>
        /// <param name="input">The input multi-channel audio buffer.</param>
        /// <returns>A mono array containing the average of all channels.</returns>
        protected float[] ConvertToMono(float[] input)
        {
            ValidateInput(input);

            int channels = Channels;
            int frames = input.Length / channels;
            float[] mono = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                float sum = 0.0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += input[i * channels + ch];
                }
                mono[i] = sum / channels;
            }

            return mono;
        }

        /// <summary>
        /// Gets the number of samples per frame (same as channel count).
        /// </summary>
        protected int SamplesPerFrame => Channels;
    }
}