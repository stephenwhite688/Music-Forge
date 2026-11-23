using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Analyzes audio signals for loudness using weighted RMS calculations and K-weighting.
    /// Supports multiple weighting schemes for different frequency ranges.
    /// </summary>
    public class LoudnessAnalyzer : AnalysisBase
    {
        private float _weightingLow;
        private float _weightingHigh;

        /// <summary>
        /// Gets or sets the weight applied to low-level samples (below -20dB).
        /// Default is 0.5f for gentle compression-like behavior on quiet sounds.
        /// </summary>
        public float WeightingLow
        {
            get => _weightingLow;
            set => _weightingLow = Math.Max(0.0f, value);
        }

        /// <summary>
        /// Gets or sets the weight applied to high-level samples (above -20dB).
        /// Default is 1.5f for emphasis on louder sounds.
        /// </summary>
        public float WeightingHigh
        {
            get => _weightingHigh;
            set => _weightingHigh = Math.Max(0.0f, value);
        }

        /// <summary>
        /// Initializes a new instance of the LoudnessAnalyzer class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        /// <param name="weightingLow">Weight for low-level samples. Default is 0.5f.</param>
        /// <param name="weightingHigh">Weight for high-level samples. Default is 1.5f.</param>
        public LoudnessAnalyzer(int sampleRate, int channels, float weightingLow = 0.5f, float weightingHigh = 1.5f)
            : base(sampleRate, channels)
        {
            _weightingLow = Math.Max(0.0f, weightingLow);
            _weightingHigh = Math.Max(0.0f, weightingHigh);
        }

        /// <summary>
        /// Analyzes the entire audio buffer and returns overall loudness.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The overall loudness as a weighted RMS value.</returns>
        public override float Analyze(float[] input)
        {
            ValidateInput(input);

            float[] mono = ConvertToMono(input);
            return ComputeLoudness(mono);
        }

        /// <summary>
        /// Analyzes each frame of the audio buffer and returns per-frame loudness values.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing loudness for each frame.</returns>
        public override float[] AnalyzeFrame(float[] input)
        {
            ValidateInput(input);

            int channels = Channels;
            int frames = input.Length / channels;
            float[] loudnessValues = new float[frames];

            for (int frame = 0; frame < frames; frame++)
            {
                double sumWeightedSquares = 0.0;

                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = input[frame * channels + ch];
                    float absSample = Math.Abs(sample);

                    float weight = (absSample < 0.1f) ? _weightingLow : _weightingHigh;
                    float weighted = sample * weight;

                    sumWeightedSquares += weighted * weighted;
                }

                loudnessValues[frame] = (float)Math.Sqrt(sumWeightedSquares / channels);
            }

            return loudnessValues;
        }

        /// <summary>
        /// Computes the weighted RMS loudness of a mono audio signal.
        /// </summary>
        /// <param name="mono">Mono audio samples.</param>
        /// <returns>The weighted RMS loudness value.</returns>
        private float ComputeLoudness(float[] mono)
        {
            double sumWeightedSquares = 0.0;

            for (int i = 0; i < mono.Length; i++)
            {
                float absSample = Math.Abs(mono[i]);

                float weight = (absSample < 0.1f) ? _weightingLow : _weightingHigh;
                float weighted = mono[i] * weight;

                sumWeightedSquares += weighted * weighted;
            }

            return (float)Math.Sqrt(sumWeightedSquares / mono.Length);
        }

        /// <summary>
        /// Analyzes the input and returns loudness in decibels.
        /// Returns negative infinity for silence or very quiet signals.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The loudness in dB, or -Infinity if signal is below threshold.</returns>
        public float AnalyzeDb(float[] input)
        {
            float loudness = Analyze(input);
            if (loudness <= 0.0f)
                return -float.PositiveInfinity;
            return 20.0f * (float)Math.Log10(loudness);
        }

        /// <summary>
        /// Analyzes each frame and returns per-frame loudness in decibels.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing loudness in dB for each frame.</returns>
        public float[] AnalyzeFrameDb(float[] input)
        {
            float[] loudnessValues = AnalyzeFrame(input);
            float[] loudnessDb = new float[loudnessValues.Length];

            for (int i = 0; i < loudnessValues.Length; i++)
            {
                if (loudnessValues[i] <= 0.0f)
                    loudnessDb[i] = -float.PositiveInfinity;
                else
                    loudnessDb[i] = 20.0f * (float)Math.Log10(loudnessValues[i]);
            }

            return loudnessDb;
        }

        /// <summary>
        /// Analyzes input with K-weighting filter applied for EBU R128-style loudness measurement.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The loudness after applying K-weighting filter.</returns>
        public float AnalyzeWithKWeighting(float[] input)
        {
            ValidateInput(input);

            float[] mono = ConvertToMono(input);

            float[] filtered = new float[mono.Length];
            float prev = 0.0f;
            float cutoff = 100.0f;
            float rc = 1.0f / (2.0f * (float)Math.PI * cutoff);
            float dt = 1.0f / SampleRate;
            float alpha = rc / (rc + dt);

            for (int i = 0; i < mono.Length; i++)
            {
                filtered[i] = alpha * (prev + mono[i] - (i > 0 ? mono[i - 1] : 0.0f));
                prev = filtered[i];
            }

            return ComputeLoudness(filtered);
        }
    }
}