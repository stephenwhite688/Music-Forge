using System;

namespace AudioForge.DSP.Analysis
{
    public enum WindowType
    {
        Rectangular,
        Hamming,
        Hann,
        BlackmanHarris
    }

    public static class WindowFunctions
    {
        /// <summary>
        /// Generates a pre-calculated array of window coefficients for a specific FFT size.
        /// </summary>
        /// <param name="type">The shape curve of the window function.</param>
        /// <param name="size">The size of the window (typically 256 to 16384).</param>
        /// <returns>A float array containing the window coefficients.</returns>
        public static float[] GenerateProfile(WindowType type, int size)
        {
            if (size <= 0)
                throw new ArgumentException("Window size must be greater than zero.", nameof(size));

            float[] window = new float[size];
            int n = size - 1;

            switch (type)
            {
                case WindowType.Rectangular:
                    // Simple pass-through window
                    for (int i = 0; i < size; i++)
                    {
                        window[i] = 1.0f;
                    }
                    break;

                case WindowType.Hamming:
                    // Alpha = 0.54, Beta = 0.46
                    for (int i = 0; i < size; i++)
                    {
                        window[i] = 0.54f - 0.46f * MathF.Cos((2f * MathF.PI * i) / n);
                    }
                    break;

                case WindowType.Hann:
                    // Raised cosine window
                    for (int i = 0; i < size; i++)
                    {
                        window[i] = 0.5f * (1f - MathF.Cos((2f * MathF.PI * i) / n));
                    }
                    break;

                case WindowType.BlackmanHarris:
                    // 4-term high-dynamic-range window (Excellent for forensic side-lobe suppression)
                    const float a0 = 0.35875f;
                    const float a1 = 0.48829f;
                    const float a2 = 0.14128f;
                    const float a3 = 0.01168f;

                    for (int i = 0; i < size; i++)
                    {
                        window[i] = a0
                                  - a1 * MathF.Cos((2f * MathF.PI * i) / n)
                                  + a2 * MathF.Cos((4f * MathF.PI * i) / n)
                                  - a3 * MathF.Cos((6f * MathF.PI * i) / n);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported window function type.");
            }

            return window;
        }
    }
}
