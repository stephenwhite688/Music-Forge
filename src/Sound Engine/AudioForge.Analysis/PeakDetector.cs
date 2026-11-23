using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Analyzes audio signals to detect peak amplitude values across the entire signal or per frame.
    /// </summary>
    public class PeakDetector : AnalysisBase
    {
        /// <summary>
        /// Initializes a new instance of the PeakDetector class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        public PeakDetector(int sampleRate, int channels)
            : base(sampleRate, channels)
        {
        }

        /// <summary>
        /// Analyzes the entire audio buffer and returns the overall peak amplitude.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The peak absolute sample value in the range [0, 1].</returns>
        public override float Analyze(float[] input)
        {
            ValidateInput(input);

            float peak = 0.0f;

            for (int i = 0; i < input.Length; i++)
            {
                float absSample = Math.Abs(input[i]);
                if (absSample > peak)
                    peak = absSample;
            }

            return peak;
        }

        /// <summary>
        /// Analyzes each frame and returns per-frame peak amplitudes.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing the peak amplitude for each frame.</returns>
        public override float[] AnalyzeFrame(float[] input)
        {
            ValidateInput(input);

            int channels = Channels;
            int frames = input.Length / channels;
            float[] peaks = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                float framePeak = 0.0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    float absSample = Math.Abs(input[i * channels + ch]);
                    if (absSample > framePeak)
                        framePeak = absSample;
                }
                peaks[i] = framePeak;
            }

            return peaks;
        }

        /// <summary>
        /// Analyzes the input and returns peak amplitude in decibels.
        /// Returns negative infinity for signals with zero or near-zero peak.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The peak in dB, or -Infinity if signal is silent.</returns>
        public float AnalyzeDb(float[] input)
        {
            float peak = Analyze(input);
            if (peak <= 0.0f)
                return -float.PositiveInfinity;
            return 20.0f * (float)Math.Log10(peak);
        }

        /// <summary>
        /// Analyzes each frame and returns per-frame peak amplitude in decibels.
        /// </summary>
        /// <param name="input">The input audio buffer containing multiple frames.</param>
        /// <returns>An array of float values representing peak amplitude in dB for each frame.</returns>
        public float[] AnalyzeFrameDb(float[] input)
        {
            float[] peaks = AnalyzeFrame(input);
            float[] peaksDb = new float[peaks.Length];

            for (int i = 0; i < peaks.Length; i++)
            {
                if (peaks[i] <= 0.0f)
                    peaksDb[i] = -float.PositiveInfinity;
                else
                    peaksDb[i] = 20.0f * (float)Math.Log10(peaks[i]);
            }

            return peaksDb;
        }
    }
}