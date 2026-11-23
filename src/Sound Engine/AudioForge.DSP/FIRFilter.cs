using System;

namespace AudioForge.DSP
{
    public class FIRFilter
    {
        private readonly float[] _coefficients;
        private readonly float[] _delay;
        private int _position;

        public int Order => _coefficients.Length - 1;
        public int Length => _coefficients.Length;

        public FIRFilter(float[] coefficients)
        {
            if (coefficients == null)
                throw new ArgumentNullException(nameof(coefficients));

            if (coefficients.Length == 0)
                throw new ArgumentException("Coefficients cannot be empty.", nameof(coefficients));

            _coefficients = new float[coefficients.Length];
            Array.Copy(coefficients, _coefficients, coefficients.Length);
            _delay = new float[coefficients.Length];
            _position = 0;
        }

        public float ProcessSample(float x)
        {
            _delay[_position] = x;

            float y = 0.0f;
            int idx = _position;

            for (int i = 0; i < _coefficients.Length; i++)
            {
                y += _coefficients[i] * _delay[idx];
                idx--;
                if (idx < 0)
                    idx = _coefficients.Length - 1;
            }

            _position = (_position + 1) % _coefficients.Length;

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
            Array.Clear(_delay, 0, _delay.Length);
            _position = 0;
        }

        public void UpdateCoefficients(float[] newCoefficients)
        {
            if (newCoefficients == null)
                throw new ArgumentNullException(nameof(newCoefficients));

            if (newCoefficients.Length != _coefficients.Length)
                throw new ArgumentException("New coefficients must have the same length.", nameof(newCoefficients));

            Array.Copy(newCoefficients, _coefficients, newCoefficients.Length);
            Reset();
        }
    }
}
