using System;

namespace AudioForge.DSP
{
    public static class DSPMath
    {
        private const float PI = (float)Math.PI;
        private const float TWO_PI = 2.0f * PI;
        private const float HALF_PI = PI / 2.0f;

        public static float Clamp(float v, float min, float max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp(t, 0.0f, 1.0f);
        }

        public static float DbToLinear(float db)
        {
            return (float)Math.Pow(10.0, db / 20.0);
        }

        public static float LinearToDb(float linear)
        {
            if (linear <= 0.0f)
                return -float.PositiveInfinity;
            return 20.0f * (float)Math.Log10(linear);
        }

        public static float FastSin(float x)
        {
            x = x % TWO_PI;
            if (x > PI) x -= TWO_PI;
            if (x < -PI) x += TWO_PI;

            float x2 = x * x;
            float x3 = x2 * x;
            float x5 = x3 * x2;
            float x7 = x5 * x2;
            
            return x - x3 / 6.0f + x5 / 120.0f - x7 / 5040.0f;
        }

        public static float FastCos(float x)
        {
            x = x % TWO_PI;
            if (x > PI) x -= TWO_PI;
            if (x < -PI) x += TWO_PI;

            float x2 = x * x;
            float x4 = x2 * x2;
            float x6 = x4 * x2;
            
            return 1.0f - x2 / 2.0f + x4 / 24.0f - x6 / 720.0f;
        }

        public static float FastTan(float x)
        {
            return FastSin(x) / FastCos(x);
        }

        public static float FastSqrt(float x)
        {
            if (x <= 0.0f) return 0.0f;
            
            float guess = x / 2.0f;
            for (int i = 0; i < 3; i++)
            {
                guess = (guess + x / guess) / 2.0f;
            }
            return guess;
        }

        public static float FastInvSqrt(float x)
        {
            if (x <= 0.0f) return float.PositiveInfinity;
            
            float x2 = x * 0.5f;
            int i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3759df - (i >> 1);
            float y = BitConverter.Int32BitsToSingle(i);
            
            y = y * (1.5f - x2 * y * y);
            y = y * (1.5f - x2 * y * y);
            
            return y;
        }

        public static float RMS(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0.0f;

            double sum = 0.0;
            for (int i = 0; i < buffer.Length; i++)
            {
                sum += buffer[i] * buffer[i];
            }
            return (float)Math.Sqrt(sum / buffer.Length);
        }

        public static float Peak(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0.0f;

            float peak = 0.0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float abs = Math.Abs(buffer[i]);
                if (abs > peak)
                    peak = abs;
            }
            return peak;
        }

        public static float Sum(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0.0f;

            float sum = 0.0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                sum += buffer[i];
            }
            return sum;
        }

        public static float Mean(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0.0f;

            return Sum(buffer) / buffer.Length;
        }

        public static float Normalize(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return 0.0f;

            float peak = Peak(buffer);
            if (peak > 0.0f)
            {
                float gain = 1.0f / peak;
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] *= gain;
                }
            }
            return peak;
        }
    }
}
