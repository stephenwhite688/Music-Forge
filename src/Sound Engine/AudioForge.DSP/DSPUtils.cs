using System;

namespace AudioForge.DSP
{
    public static class DSPUtils
    {
        public static float[] Mix(float[] a, float[] b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException();

            int maxLength = Math.Max(a.Length, b.Length);
            float[] result = new float[maxLength];

            for (int i = 0; i < maxLength; i++)
            {
                float sampleA = i < a.Length ? a[i] : 0.0f;
                float sampleB = i < b.Length ? b[i] : 0.0f;
                result[i] = sampleA + sampleB;
            }

            return result;
        }

        public static float[] Add(float[] a, float[] b)
        {
            return Mix(a, b);
        }

        public static float[] Multiply(float[] a, float[] b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException();

            int maxLength = Math.Max(a.Length, b.Length);
            float[] result = new float[maxLength];

            for (int i = 0; i < maxLength; i++)
            {
                float sampleA = i < a.Length ? a[i] : 0.0f;
                float sampleB = i < b.Length ? b[i] : 0.0f;
                result[i] = sampleA * sampleB;
            }

            return result;
        }

        public static float[] Scale(float[] a, float gain)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            float[] result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * gain;
            }
            return result;
        }

        public static float[] Copy(float[] a)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            float[] result = new float[a.Length];
            Array.Copy(a, result, a.Length);
            return result;
        }

        public static float[] Zero(int length)
        {
            if (length < 0)
                throw new ArgumentException("Length cannot be negative.", nameof(length));

            return new float[length];
        }

        public static float[] Clone(float[] a)
        {
            return Copy(a);
        }

        public static float[] Reverse(float[] a)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            float[] result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[a.Length - 1 - i];
            }
            return result;
        }

        public static float[] Abs(float[] a)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            float[] result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = Math.Abs(a[i]);
            }
            return result;
        }

        public static float[] Normalize(float[] a)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            float peak = DSPMath.Peak(a);
            if (peak > 0.0f)
            {
                return Scale(a, 1.0f / peak);
            }
            return Copy(a);
        }

        public static float[] FadeIn(float[] a, int fadeLength)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            if (fadeLength <= 0)
                return Copy(a);

            float[] result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                float gain = i < fadeLength ? (float)i / fadeLength : 1.0f;
                result[i] = a[i] * gain;
            }
            return result;
        }

        public static float[] FadeOut(float[] a, int fadeLength)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            if (fadeLength <= 0)
                return Copy(a);

            float[] result = new float[a.Length];
            int startFade = Math.Max(0, a.Length - fadeLength);

            for (int i = 0; i < a.Length; i++)
            {
                float gain = i >= startFade ? 1.0f - (float)(i - startFade) / fadeLength : 1.0f;
                result[i] = a[i] * gain;
            }
            return result;
        }

        public static float[] CrossFade(float[] a, float[] b, int fadeLength)
        {
            if (a == null || b == null)
                throw new ArgumentNullException();

            int maxLength = Math.Max(a.Length, b.Length);
            float[] result = new float[maxLength];
            int crossFadeStart = Math.Max(0, a.Length - fadeLength);

            for (int i = 0; i < maxLength; i++)
            {
                float sampleA = i < a.Length ? a[i] : 0.0f;
                float sampleB = i < b.Length ? b[i] : 0.0f;

                if (i < crossFadeStart)
                {
                    result[i] = sampleA;
                }
                else if (i >= crossFadeStart && i < a.Length)
                {
                    float t = (float)(i - crossFadeStart) / fadeLength;
                    float gainA = 1.0f - t;
                    float gainB = t;
                    result[i] = sampleA * gainA + sampleB * gainB;
                }
                else
                {
                    result[i] = sampleB;
                }
            }

            return result;
        }
    }
}
