using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Analyzes audio signals to compute root mean square (RMS) values and convert them to decibels.
    /// Supports per-frame analysis for real-time metering applications.
    /// </summary>
    public class RMSAnalyzer : AnalysisBase
    {
        /// <summary>
        /// Initializes a new instance of the RMSAnalyzer class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        public RMSAnalyzer(int sampleRate, int channels)
            : base(sampleRate, channels)
        {
        }

        /// <summary>
        /// Analyzes the entire audio buffer and returns the overall RMS value.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The RMS amplitude in the range [0, 1].</returns>
        public override float Analyze(float[] input)
        {
            ValidateInput(input);

            double sumSquares = 0.0;

            for (int i = 0; i < input.Length; i++)
            {
                sumSquares += input[i] * input[i];
            }

            return (float)Math.Sqrt(sumSquares / input.Length);
        }

        /// <summary>
        /// Analyzes each frame and returns per-frame RMS values.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing RMS for each frame.</returns>
        public override float[] AnalyzeFrame(float[] input)
        {
            ValidateInput(input);

            int channels = Channels;
            int frames = input.Length / channels;
            float[] rmsValues = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                double sumSquares = 0.0;
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = input[i * channels + ch];
                    sumSquares += sample * sample;
                }
                rmsValues[i] = (float)Math.Sqrt(sumSquares / channels);
            }

            return rmsValues;
        }

        /// <summary>
        /// Analyzes the input and returns RMS in decibels.
        /// Returns negative infinity for signals with zero or near-zero RMS.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The RMS in dB, or -Infinity if signal is silent.</returns>
        public float AnalyzeDb(float[] input)
        {
            float rms = Analyze(input);
            if (rms <= 0.0f)
                return -float.PositiveInfinity;
            return 20.0f * (float)Math.Log10(rms);
        }

        /// <summary>
        /// Analyzes each frame and returns per-frame RMS in decibels.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing RMS in dB for each frame.</returns>
        public float[] AnalyzeFrameDb(float[] input)
        {
            float[] rmsValues = AnalyzeFrame(input);
            float[] rmsDb = new float[rmsValues.Length];

            for (int i = 0; i < rmsValues.Length; i++)
            {
                if (rmsValues[i] <= 0.0f)
                    rmsDb[i] = -float.PositiveInfinity;
                else
                    rmsDb[i] = 20.0f * (float)Math.Log10(rmsValues[i]);
            }

            return rmsDb;
        }

        /// <summary>
        /// Analyzes the input after converting it to mono, then returns RMS value.
        /// </summary>
        /// <param name="input">The input multi-channel audio buffer.</param>
        /// <returns>The RMS amplitude of the mono-converted signal.</returns>
        public float AnalyzeMono(float[] input)
        {
            float[] mono = ConvertToMono(input);
            return Analyze(mono);
        }
    }
}