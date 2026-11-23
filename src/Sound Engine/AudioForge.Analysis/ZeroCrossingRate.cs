using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Analyzes audio signals to compute zero-crossing rates, which indicate signal complexity and timbre.
    /// High zero-crossing rates suggest complex sounds (e.g., noise, cymbals), while low rates indicate tonal sounds (e.g., bass).
    /// </summary>
    public class ZeroCrossingRate : AnalysisBase
    {
        /// <summary>
        /// Initializes a new instance of the ZeroCrossingRate class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        public ZeroCrossingRate(int sampleRate, int channels)
            : base(sampleRate, channels)
        {
        }

        /// <summary>
        /// Analyzes the entire audio buffer and returns the overall zero-crossing rate.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The zero-crossing rate normalized to range [0, 1].</returns>
        public override float Analyze(float[] input)
        {
            ValidateInput(input);

            float[] mono = ConvertToMono(input);
            return ComputeZeroCrossings(mono);
        }

        /// <summary>
        /// Analyzes each frame and returns per-frame zero-crossing rates.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing zero-crossing rate for each frame.</returns>
        public override float[] AnalyzeFrame(float[] input)
        {
            ValidateInput(input);

            int channels = Channels;
            int frames = input.Length / channels;
            float[] results = new float[frames];

            for (int frame = 0; frame < frames; frame++)
            {
                int crossings = 0;
                float prevSample = 0.0f;

                for (int ch = 0; ch < channels; ch++)
                {
                    float currentSample = input[frame * channels + ch];

                    if ((prevSample < 0 && currentSample > 0) ||
                        (prevSample > 0 && currentSample < 0) ||
                        (prevSample == 0 && currentSample != 0))
                    {
                        crossings++;
                    }

                    prevSample = currentSample;
                }

                results[frame] = (float)crossings / channels;
            }

            return results;
        }

        /// <summary>
        /// Computes the zero-crossing rate for a mono audio signal.
        /// </summary>
        /// <param name="mono">Mono audio samples.</param>
        /// <returns>The zero-crossing rate normalized to range [0, 1].</returns>
        private float ComputeZeroCrossings(float[] mono)
        {
            if (mono.Length <= 1)
                return 0.0f;

            int crossings = 0;

            for (int i = 1; i < mono.Length; i++)
            {
                if ((mono[i - 1] < 0 && mono[i] > 0) ||
                    (mono[i - 1] > 0 && mono[i] < 0) ||
                    (mono[i - 1] == 0 && mono[i] != 0))
                {
                    crossings++;
                }
            }

            return (float)crossings / mono.Length;
        }

        /// <summary>
        /// Analyzes the input and returns zero-crossing rate in crossings per second.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The zero-crossing rate in Hz (crossings per second).</returns>
        public float AnalyzePerSecond(float[] input)
        {
            float rate = Analyze(input);
            return rate * SampleRate;
        }

        /// <summary>
        /// Analyzes each frame and returns per-frame zero-crossing rates in crossings per second.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing zero-crossing rate in Hz for each frame.</returns>
        public float[] AnalyzeFramePerSecond(float[] input)
        {
            float[] rates = AnalyzeFrame(input);
            float[] ratesPerSecond = new float[rates.Length];

            for (int i = 0; i < rates.Length; i++)
            {
                ratesPerSecond[i] = rates[i] * SampleRate;
            }

            return ratesPerSecond;
        }
    }
}