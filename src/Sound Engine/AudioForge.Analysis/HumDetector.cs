using System;
using AudioForge.DSP.Analysis;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Detects continuous mains-frequency hum and its harmonics - a standard
    /// forensic check for recordings that picked up AC interference. Finds
    /// the strongest low-frequency tonal component, then reports its
    /// fundamental frequency, harmonic amplitudes and a confidence figure.
    /// </summary>
    public static class HumDetector
    {
        /// <summary>One detected tonal component.</summary>
        public sealed class Tone
        {
            public double FrequencyHz { get; init; }
            public double AmplitudeDb { get; init; }
            /// <summary>Harmonic number (1 = fundamental).</summary>
            public int Harmonic { get; init; }
        }

        /// <summary>Hum analysis result.</summary>
        public sealed class Report
        {
            /// <summary>Detected fundamental (50 or 60 Hz region), 0 if none.</summary>
            public double FundamentalHz { get; init; }
            /// <summary>Peak harmonic amplitude in dBFS, -120 if none.</summary>
            public double PeakAmplitudeDb { get; init; }
            /// <summary>Number of harmonics detected above threshold.</summary>
            public int HarmonicCount { get; init; }
            /// <summary>0..1 confidence that a hum is present.</summary>
            public double Confidence { get; init; }
            public Tone[] Tones { get; init; } = Array.Empty<Tone>();
        }

        private const double MinFundamental = 45.0;
        private const double MaxFundamental = 65.0;
        private const int MaxHarmonics = 10;
        private const double PeakThresholdDb = -60.0;

        /// <summary>
        /// Analyses a mono downmix of the buffer for mains hum.
        /// </summary>
        /// <param name="interleaved">Interleaved PCM.</param>
        /// <param name="channels">Channel count.</param>
        /// <param name="sampleRate">Sample rate.</param>
        /// <param name="fftSize">FFT size (power of two, default 8192 for ~6 Hz resolution at 48 kHz).</param>
        public static Report Analyze(float[] interleaved, int channels, int sampleRate, int fftSize = 8192)
        {
            if (interleaved is null) throw new ArgumentNullException(nameof(interleaved));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (fftSize < 1024 || (fftSize & (fftSize - 1)) != 0)
                throw new ArgumentException("FFT size must be a power of two >= 1024.", nameof(fftSize));

            // Use up to fftSize frames from the middle of the buffer for the
            // spectral estimate (avoids edge transients).
            long totalFrames = interleaved.Length / channels;
            int frames = (int)Math.Min(totalFrames, fftSize);
            if (frames < fftSize / 2) frames = (int)totalFrames;
            if (frames < 256) return new Report();

            var mono = new float[frames];
            long startFrame = Math.Max(0, (totalFrames - frames) / 2);
            for (int i = 0; i < frames; i++)
            {
                double sum = 0;
                int offset = (int)((startFrame + i) * channels);
                for (int ch = 0; ch < channels; ch++) sum += interleaved[offset + ch];
                mono[i] = (float)(sum / channels);
            }

            // Hann window + FFT
            var real = new float[fftSize];
            var imag = new float[fftSize];
            for (int i = 0; i < frames; i++)
                real[i] = mono[i] * (0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / (frames - 1)));
            FFT.Compute(real, imag);

            double binHz = (double)sampleRate / fftSize;
            int fundamentalBin = (int)Math.Round(50.0 / binHz);
            int searchBins = (int)Math.Round((MaxFundamental - MinFundamental) / binHz);

            // Find strongest peak in 45-65 Hz region
            int bestBin = -1;
            float bestMag = 0f;
            for (int b = Math.Max(1, fundamentalBin - searchBins); b <= fundamentalBin + searchBins && b < fftSize / 2; b++)
            {
                float mag = Magnitude(real, imag, b, fftSize);
                if (mag > bestMag) { bestMag = mag; bestBin = b; }
            }
            if (bestBin < 0) return new Report();

            double fundHz = bestBin * binHz;
            // Refine: only accept if close to 50 or 60 Hz family (±3 Hz)
            bool near50 = Math.Abs(fundHz - 50.0) < 3.0 || Math.Abs(fundHz - 100.0 / 2) < 3.0;
            bool near60 = Math.Abs(fundHz - 60.0) < 3.0;
            if (!near50 && !near60) return new Report();

            // Measure harmonics
            var tones = new System.Collections.Generic.List<Tone>();
            double peakDb = -120;
            for (int h = 1; h <= MaxHarmonics; h++)
            {
                double hz = fundHz * h;
                if (hz >= sampleRate / 2.0) break;
                int hb = (int)Math.Round(hz / binHz);
                if (hb < 1 || hb >= fftSize / 2) continue;
                // Peak-pick within ±1 bin
                float mag = 0f;
                for (int d = -1; d <= 1; d++)
                {
                    int b = hb + d;
                    if (b >= 1 && b < fftSize / 2)
                        mag = Math.Max(mag, Magnitude(real, imag, b, fftSize));
                }
                double db = mag > 1e-7 ? 20.0 * Math.Log10(mag) : -120.0;
                if (db > PeakThresholdDb)
                {
                    tones.Add(new Tone { FrequencyHz = hz, AmplitudeDb = db, Harmonic = h });
                    if (db > peakDb) peakDb = db;
                }
            }

            if (tones.Count == 0) return new Report();

            // Confidence: harmonic count + peak level above threshold
            double confidence = Math.Min(1.0, tones.Count / 4.0 + Math.Max(0, peakDb - PeakThresholdDb) / 40.0);
            return new Report
            {
                FundamentalHz = fundHz,
                PeakAmplitudeDb = peakDb,
                HarmonicCount = tones.Count,
                Confidence = Math.Round(confidence, 3),
                Tones = tones.ToArray()
            };
        }

        private static float Magnitude(float[] real, float[] imag, int bin, int n)
        {
            float m = MathF.Sqrt(real[bin] * real[bin] + imag[bin] * imag[bin]) / n;
            return bin == 0 || bin == n / 2 - 1 ? m : m * 2f;
        }
    }
}
