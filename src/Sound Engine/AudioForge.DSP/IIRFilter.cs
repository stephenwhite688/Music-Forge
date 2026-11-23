using System;

namespace AudioForge.DSP
{
    public class IIRFilter
    {
        private readonly float[] _a;
        private readonly float[] _b;
        private readonly float[] _x;
        private readonly float[] _y;
        private int _position;

        public int Order => Math.Max(_a.Length, _b.Length) - 1;

        public IIRFilter(float[] a, float[] b)
        {
            if (a == null || b == null)
                throw new ArgumentNullException();

            if (a.Length == 0 || b.Length == 0)
                throw new ArgumentException("Coefficients cannot be empty.");

            float a0 = a[0];
            _a = new float[a.Length];
            _b = new float[b.Length];

            for (int i = 0; i < a.Length; i++)
                _a[i] = a[i] / a0;

            for (int i = 0; i < b.Length; i++)
                _b[i] = b[i] / a0;

            int maxOrder = Math.Max(_a.Length, _b.Length);
            _x = new float[maxOrder];
            _y = new float[maxOrder];
            _position = 0;
        }

        public float ProcessSample(float x)
        {
            _x[_position] = x;

            float y = 0.0f;

            for (int i = 0; i < _b.Length; i++)
            {
                int idx = (_position - i + _x.Length) % _x.Length;
                y += _b[i] * _x[idx];
            }

            for (int i = 1; i < _a.Length; i++)
            {
                int idx = (_position - i + _y.Length) % _y.Length;
                y -= _a[i] * _y[idx];
            }

            _y[_position] = y;
            _position = (_position + 1) % _x.Length;

            return y;
        }

        public float[] Process(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            float[] output = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = ProcessSample(input[i]);
            }
            return output;
        }

        public void Reset()
        {
            Array.Clear(_x, 0, _x.Length);
            Array.Clear(_y, 0, _y.Length);
            _position = 0;
        }
    }
}
