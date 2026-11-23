using System;
using AudioForge.DSP.Analysis;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Estimates the stationary noise floor of a recording using Welch-style
    /// averaged periodograms. Produces a smoothed dBFS noise spectrum that
    /// forensic workflows use as the baseline for spectral-subtraction and
    /// signal-to-noise assessment.
    /// </summary>
    public static class NoiseFloorEstimator
    {
        /// <summary>A noise-floor spectrum estimate.</summary>
        public sealed class Report
        {
            public int SampleRate { get; init; }
            public int FftSize { get; init; }
            /// <summary>Hz per bin.</summary>
            public double BinWidthHz { get; init; }
            /// <summary>Averaged magnitude spectrum in dBFS, one per bin.</summary>
            public float[] SpectrumDb { get; init; } = Array.Empty<float>();
            /// <summary>Overall noise floor estimate in dBFS (median of spectrum).</summary>
            public double NoiseFloorDb { get; init; }
            /// <summary>Number of averaged frames used.</summary>
            public int FramesUsed { get; init; }
        }

        /// <summary>
        /// Estimates the noise floor from interleaved PCM (mono-mixed).
        /// Long files are analysed using up to <paramref name="maxSeconds"/>
        /// sampled evenly across the file.
        /// </summary>
        /// <param name="interleaved">Interleaved PCM.</param>
        /// <param name="channels">Channel count.</param>
        /// <param name="sampleRate">Sample rate.</param>
        /// <param name="fftSize">FFT size (power of two, default 4096).</param>
        /// <param name="maxSeconds">Cap on analysed audio (default 30 s).</param>
        public static Report Analyze(float[] interleaved, int channels, int sampleRate,
            int fftSize = 4096, double maxSeconds = 30.0)
        {
            if (interleaved is null) throw new ArgumentNullException(nameof(interleaved));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (fftSize < 1024 || (fftSize & (fftSize - 1)) != 0)
                throw new ArgumentException("FFT size must be a power of two >= 1024.", nameof(fftSize));

            long totalFrames = interleaved.Length / channels;
            long maxFrames = (long)(sampleRate * maxSeconds);
            // Sample evenly-spaced windows across the file
            int windows = (int)Math.Max(1, Math.Min(maxFrames / fftSize, 64));
            long stride = Math.Max(fftSize, (totalFrames - fftSize) / Math.Max(1, windows - 1));

            var window = new float[fftSize];
            for (int i = 0; i < fftSize; i++)
                window[i] = 0.5f - 0.5f * MathF.Cos(2f * MathF.PI * i / (fftSize - 1));

            var real = new float[fftSize];
            var imag = new float[fftSize];
            var powerSum = new double[fftSize / 2];
            int framesUsed = 0;

            for (long start = 0; start + fftSize <= totalFrames && framesUsed < windows; start += stride)
            {
                Array.Clear(real);
                Array.Clear(imag);
                for (int i = 0; i < fftSize; i++)
                {
                    double sum = 0;
                    int offset = (int)((start + i) * channels);
                    for (int ch = 0; ch < channels; ch++) sum += interleaved[offset + ch];
                    real[i] = (float)(sum / channels) * window[i];
                }
                FFT.Compute(real, imag);
                for (int b = 0; b < fftSize / 2; b++)
                {
                    double p = real[b] * real[b] + imag[b] * imag[b];
                    powerSum[b] += p;
                }
                framesUsed++;
            }
            if (framesUsed == 0) return new Report { SampleRate = sampleRate, FftSize = fftSize };

            var spectrumDb = new float[fftSize / 2];
            var sorted = new double[spectrumDb.Length];
            for (int b = 0; b < spectrumDb.Length; b++)
            {
                double avgPower = powerSum[b] / framesUsed;
                double mag = Math.Sqrt(avgPower) / fftSize * (b == 0 || b == spectrumDb.Length - 1 ? 1.0 : 2.0);
                double db = mag > 1e-8 ? 20.0 * Math.Log10(mag) : -120.0;
                spectrumDb[b] = (float)db;
                sorted[b] = db;
            }
            Array.Sort(sorted);

            return new Report
            {
                SampleRate = sampleRate,
                FftSize = fftSize,
                BinWidthHz = (double)sampleRate / fftSize,
                SpectrumDb = spectrumDb,
                NoiseFloorDb = sorted[sorted.Length / 2],
                FramesUsed = framesUsed
            };
        }
    }
}
