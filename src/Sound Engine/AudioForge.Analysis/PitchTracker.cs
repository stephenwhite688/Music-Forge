using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Estimates the fundamental frequency of voiced audio using the
    /// autocorrelation method (YIN-style difference function with adaptive
    /// threshold). Used in voice forensics, music analysis and tonal
    /// inspection. Processes frames and returns per-frame pitch plus an
    /// aggregate report.
    /// </summary>
    public static class PitchTracker
    {
        /// <summary>Per-frame pitch estimate.</summary>
        public sealed class FrameResult
        {
            /// <summary>Frame start time in seconds.</summary>
            public double TimeSeconds { get; init; }
            /// <summary>Estimated fundamental frequency in Hz (0 = unvoiced).</summary>
            public double FrequencyHz { get; init; }
            /// <summary>Voicing confidence 0..1.</summary>
            public double Confidence { get; init; }
        }

        /// <summary>Aggregate pitch report.</summary>
        public sealed class Report
        {
            public double MedianFrequencyHz { get; init; }
            public double MinFrequencyHz { get; init; }
            public double MaxFrequencyHz { get; init; }
            /// <summary>Percentage of frames detected as voiced.</summary>
            public double VoicedPercent { get; init; }
            public FrameResult[] Frames { get; init; } = Array.Empty<FrameResult>();
        }

        /// <summary>
        /// Tracks pitch across the buffer. Frames are windowed with Hann and
        /// analysed via normalized autocorrelation.
        /// </summary>
        /// <param name="interleaved">Interleaved PCM.</param>
        /// <param name="channels">Channel count (mixed to mono).</param>
        /// <param name="sampleRate">Sample rate.</param>
        /// <param name="frameMs">Frame length in milliseconds (default 46 ms ≈ 2048 @ 44.1k).</param>
        /// <param name="hopMs">Hop between frames (default 23 ms ≈ 50% overlap).</param>
        /// <param name="minFreq">Minimum detectable pitch (default 60 Hz).</param>
        /// <param name="maxFreq">Maximum detectable pitch (default 1000 Hz).</param>
        public static Report Analyze(float[] interleaved, int channels, int sampleRate,
            double frameMs = 46.0, double hopMs = 23.0,
            double minFreq = 60.0, double maxFreq = 1000.0)
        {
            if (interleaved is null) throw new ArgumentNullException(nameof(interleaved));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

            int frameSize = Math.Max(256, (int)(sampleRate * frameMs / 1000.0));
            int hop = Math.Max(64, (int)(sampleRate * hopMs / 1000.0));
            long totalFrames = interleaved.Length / channels;
            if (totalFrames < frameSize) return new Report();

            int maxLag = Math.Min(frameSize / 2, (int)(sampleRate / minFreq));
            int minLag = Math.Max(2, (int)(sampleRate / maxFreq));
            var window = new float[frameSize];
            for (int i = 0; i < frameSize; i++)
                window[i] = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / (frameSize - 1));

            var mono = new float[frameSize];
            var results = new System.Collections.Generic.List<FrameResult>();

            for (long start = 0; start + frameSize <= totalFrames; start += hop)
            {
                // Mix to mono
                double rms = 0;
                for (int i = 0; i < frameSize; i++)
                {
                    double sum = 0;
                    int offset = (int)((start + i) * channels);
                    for (int ch = 0; ch < channels; ch++) sum += interleaved[offset + ch];
                    mono[i] = (float)(sum / channels) * window[i];
                    rms += mono[i] * mono[i];
                }
                rms = Math.Sqrt(rms / frameSize);
                if (rms < 0.005) // too quiet to pitch reliably
                {
                    results.Add(new FrameResult { TimeSeconds = (double)start / sampleRate });
                    continue;
                }

                // Normalized autocorrelation over lag range
                double bestCorr = -1;
                int bestLag = -1;
                for (int lag = minLag; lag <= maxLag; lag++)
                {
                    double corr = 0, e0 = 0, e1 = 0;
                    for (int i = 0; i + lag < frameSize; i++)
                    {
                        double a = mono[i], b = mono[i + lag];
                        corr += a * b; e0 += a * a; e1 += b * b;
                    }
                    if (e0 <= 1e-12 || e1 <= 1e-12) continue;
                    double norm = corr / Math.Sqrt(e0 * e1);
                    if (norm > bestCorr) { bestCorr = norm; bestLag = lag; }
                }

                if (bestLag > 0 && bestCorr > 0.3)
                {
                    double freq = (double)sampleRate / bestLag;
                    results.Add(new FrameResult
                    {
                        TimeSeconds = (double)start / sampleRate,
                        FrequencyHz = freq,
                        Confidence = Math.Min(1.0, bestCorr)
                    });
                }
                else
                {
                    results.Add(new FrameResult { TimeSeconds = (double)start / sampleRate });
                }
            }

            var voiced = results.FindAll(r => r.FrequencyHz > 0);
            var report = new Report
            {
                Frames = results.ToArray(),
                VoicedPercent = results.Count > 0 ? 100.0 * voiced.Count / results.Count : 0
            };
            if (voiced.Count > 0)
            {
                var freqs = voiced.ConvertAll(v => v.FrequencyHz);
                freqs.Sort();
                report = new Report
                {
                    MedianFrequencyHz = freqs[freqs.Count / 2],
                    MinFrequencyHz = freqs[0],
                    MaxFrequencyHz = freqs[^1],
                    VoicedPercent = report.VoicedPercent,
                    Frames = report.Frames
                };
            }
            return report;
        }
    }
}
