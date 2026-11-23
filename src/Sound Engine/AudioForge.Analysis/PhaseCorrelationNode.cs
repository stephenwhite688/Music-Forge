using AudioForge.Core;
using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Calculates the inter-channel phase correlation to determine stereo width and panning characteristics.
    /// Correlation value ranges from -1 (fully left) to +1 (fully right), with 0 indicating uncorrelated audio.
    /// </summary>
    public class PhaseCorrelationNode : AudioNode
    {
        private float _correlation;

        /// <summary>
        /// Gets the current phase correlation value between left and right channels.
        /// Range: -1 (fully left) to +1 (fully right), with 0 indicating uncorrelated audio.
        /// </summary>
        public float Correlation => _correlation;

        /// <summary>
        /// Initializes a new instance of the PhaseCorrelationNode class.
        /// Requires stereo input (2 channels).
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels. Must be 2 for stereo correlation analysis.</param>
        /// <param name="name">Optional node name. Default is "Phase Correlation".</param>
        /// <exception cref="ArgumentException">Thrown when the channel count is not 2 (stereo).</exception>
        public PhaseCorrelationNode(int sampleRate, int channels, string name = "Phase Correlation")
            : base(sampleRate, channels, name)
        {
            if (channels != 2)
                throw new ArgumentException("Phase correlation requires stereo (2 channels)", nameof(channels));
            Reset();
        }

        /// <summary>
        /// Processes the input stereo buffer and calculates phase correlation between left and right channels.
        /// Returns the original input unchanged (pass-through behavior).
        /// </summary>
        /// <param name="input">The interleaved stereo audio buffer (left channel first, then right).</param>
        public override void Process(Span<float> input)
        {
            double sumL = 0.0;
            double sumR = 0.0;
            double sumLR = 0.0;

            for (int i = 0; i < input.Length; i += 2)
            {
                float left = input[i];
                float right = input[i + 1];

                sumL += left * left;
                sumR += right * right;
                sumLR += left * right;
            }

            if (sumL > 0.0 && sumR > 0.0)
            {
                _correlation = (float)(sumLR / Math.Sqrt(sumL * sumR));
            }
            else
            {
                _correlation = 0.0f;
            }
        }

        /// <summary>
        /// Resets the phase correlation meter to zero.
        /// </summary>
        public override void Reset()
        {
            _correlation = 0.0f;
        }
    }
}