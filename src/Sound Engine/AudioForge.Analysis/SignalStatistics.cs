using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Computes forensic-grade signal statistics over a complete buffer:
    /// per-channel DC offset, sample peak, RMS, clipped-sample count and
    /// duration. These are the standard figures quoted in audio forensic
    /// reports and authenticity checks.
    /// </summary>
    public static class SignalStatistics
    {
        /// <summary>Per-channel measurement set.</summary>
        public sealed class ChannelStats
        {
            public int Channel { get; init; }
            public double DcOffset { get; init; }
            public float SamplePeak { get; init; }
            public float SamplePeakDbfs { get; init; }
            public float Rms { get; init; }
            public float RmsDbfs { get; init; }
            /// <summary>Samples at or beyond ±0.999 (hard clipped).</summary>
            public long ClippedSamples { get; init; }
            /// <summary>Clipped samples as a percentage of channel frames.</summary>
            public double ClippedPercent { get; init; }
        }

        /// <summary>Whole-file measurement report.</summary>
        public sealed class Report
        {
            public int SampleRate { get; init; }
            public int ChannelCount { get; init; }
            public double DurationSeconds { get; init; }
            public long TotalFrames { get; init; }
            public ChannelStats[] Channels { get; init; } = Array.Empty<ChannelStats>();
        }

        /// <summary>
        /// Analyses interleaved PCM. For large files prefer chunked processing
        /// via <see cref="AnalyzeChunked"/> to bound memory.
        /// </summary>
        public static Report Analyze(float[] interleaved, int channels, int sampleRate)
        {
            if (interleaved is null) throw new ArgumentNullException(nameof(interleaved));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (interleaved.Length % channels != 0)
                throw new ArgumentException("Buffer length must be a multiple of channel count.", nameof(interleaved));

            long frames = interleaved.Length / channels;
            var stats = new ChannelStats[channels];
            var sums = new double[channels];
            var sumSq = new double[channels];
            var peaks = new float[channels];
            var clips = new long[channels];

            for (long i = 0; i < frames; i++)
            {
                int offset = (int)(i * channels);
                for (int ch = 0; ch < channels; ch++)
                {
                    float v = interleaved[offset + ch];
                    sums[ch] += v;
                    sumSq[ch] += (double)v * v;
                    float a = MathF.Abs(v);
                    if (a > peaks[ch]) peaks[ch] = a;
                    if (a >= 0.999f) clips[ch]++;
                }
            }

            for (int ch = 0; ch < channels; ch++)
            {
                double dc = frames > 0 ? sums[ch] / frames : 0;
                double rms = frames > 0 ? Math.Sqrt(sumSq[ch] / frames) : 0;
                stats[ch] = new ChannelStats
                {
                    Channel = ch,
                    DcOffset = dc,
                    SamplePeak = peaks[ch],
                    SamplePeakDbfs = (float)(peaks[ch] > 1e-7f ? 20.0 * Math.Log10(peaks[ch]) : -120.0),
                    Rms = (float)rms,
                    RmsDbfs = (float)(rms > 1e-7 ? 20.0 * Math.Log10(rms) : -120.0),
                    ClippedSamples = clips[ch],
                    ClippedPercent = frames > 0 ? 100.0 * clips[ch] / frames : 0
                };
            }

            return new Report
            {
                SampleRate = sampleRate,
                ChannelCount = channels,
                DurationSeconds = (double)frames / sampleRate,
                TotalFrames = frames,
                Channels = stats
            };
        }
    }
}
