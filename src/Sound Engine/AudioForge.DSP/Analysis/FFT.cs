using System;

namespace AudioForge.DSP.Analysis
{
    /// <summary>
    /// Provides radix-2 FFT and short-time Fourier transform operations.
    /// </summary>
    public static class FFT
    {
        /// <summary>
        /// In-place Cooley-Tukey Radix-2 FFT.
        /// </summary>
        public static void Compute(float[] real, float[] imag)
        {
            if (real == null)
                throw new ArgumentNullException(nameof(real));
            if (imag == null)
                throw new ArgumentNullException(nameof(imag));
            if (real.Length != imag.Length)
                throw new ArgumentException("Real and imaginary arrays must have the same length.");
            if (real.Length < 2 || (real.Length & (real.Length - 1)) != 0)
                throw new ArgumentException("FFT input length must be a power of two.", nameof(real));

            int n = real.Length;
            int j = 0;

            for (int i = 0; i < n; i++)
            {
                if (i < j)
                {
                    float tr = real[j];
                    float ti = imag[j];
                    real[j] = real[i];
                    imag[j] = imag[i];
                    real[i] = tr;
                    imag[i] = ti;
                }

                int m = n >> 1;
                while (m >= 1 && j >= m)
                {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                float ang = -2f * MathF.PI / len;
                float wlen_r = MathF.Cos(ang);
                float wlen_i = MathF.Sin(ang);

                for (int i = 0; i < n; i += len)
                {
                    float wr = 1f;
                    float wi = 0f;

                    for (int k = 0; k < len / 2; k++)
                    {
                        int even = i + k;
                        int odd = even + len / 2;

                        float er = real[even];
                        float ei = imag[even];
                        float or = real[odd];
                        float oi = imag[odd];

                        float tr = wr * or - wi * oi;
                        float ti = wr * oi + wi * or;

                        real[even] = er + tr;
                        imag[even] = ei + ti;
                        real[odd] = er - tr;
                        imag[odd] = ei - ti;

                        float next_wr = wr * wlen_r - wi * wlen_i;
                        wi = wr * wlen_i + wi * wlen_r;
                        wr = next_wr;
                    }
                }
            }
        }

        /// <summary>
        /// Slices a continuous stream segment into overlapping windowed segments,
        /// applies a custom window profile, and performs an FFT on each slice.
        /// </summary>
        /// <param name="samples">The input audio channel samples.</param>
        /// <param name="fftSize">The window block size (256 to 16384, must be a power of two).</param>
        /// <param name="overlapPercentage">The sliding step overlap percentage (0.0f to 0.90f).</param>
        /// <param name="windowProfile">Pre-calculated window weights matching fftSize length.</param>
        /// <param name="processFrame">Callback triggered per computed frame providing (real[], imag[]).</param>
        public static void ComputeSTFT(
            float[] samples,
            int fftSize,
            float overlapPercentage,
            float[] windowProfile,
            Action<float[], float[]> processFrame)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (windowProfile == null) throw new ArgumentNullException(nameof(windowProfile));
            if (processFrame == null) throw new ArgumentNullException(nameof(processFrame));
            if (windowProfile.Length != fftSize)
                throw new ArgumentException("Window profile length must match the requested FFT size.");

            int stepSize = (int)(fftSize * (1.0f - Math.Clamp(overlapPercentage, 0.0f, 0.90f)));
            if (stepSize <= 0) stepSize = 1;

            float[] frameReal = new float[fftSize];
            float[] frameImag = new float[fftSize];

            for (int offset = 0; offset + fftSize <= samples.Length; offset += stepSize)
            {
                // Isolate frame, apply analytical windowing coefficients, and reset imaginary space
                for (int i = 0; i < fftSize; i++)
                {
                    frameReal[i] = samples[offset + i] * windowProfile[i];
                    frameImag[i] = 0f;
                }

                Compute(frameReal, frameImag);
                processFrame(frameReal, frameImag);
            }
        }

        /// <summary>
        /// Matrix transformations for Mid/Side spatial decoupling.
        /// </summary>
        public static void DeconstructMidSide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> mid, Span<float> side)
        {
            int length = Math.Min(left.Length, right.Length);
            if (mid.Length < length || side.Length < length)
                throw new ArgumentException("Destination spans are too small for the source sample size.");

            for (int i = 0; i < length; i++)
            {
                float l = left[i];
                float r = right[i];

                mid[i] = (l + r) * 0.5f;
                side[i] = (l - r) * 0.5f;
            }
        }
    }
}
