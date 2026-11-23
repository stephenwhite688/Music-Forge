using System;

namespace AudioForge.DSP
{
    public class Convolution
    {
        private readonly float[] _ir;
        private readonly float[] _buffer;
        private int _position;

        public int Length => _ir.Length;

        public Convolution(float[] impulseResponse)
        {
            if (impulseResponse == null)
                throw new ArgumentNullException(nameof(impulseResponse));

            if (impulseResponse.Length == 0)
                throw new ArgumentException("Impulse response cannot be empty.", nameof(impulseResponse));

            _ir = new float[impulseResponse.Length];
            Array.Copy(impulseResponse, _ir, impulseResponse.Length);
            _buffer = new float[impulseResponse.Length];
            _position = 0;
        }

        public float ProcessSample(float x)
        {
            _buffer[_position] = x;

            float y = 0.0f;
            int pos = _position;

            for (int i = 0; i < _ir.Length; i++)
            {
                int idx = pos - i;
                if (idx < 0)
                    idx += _ir.Length;
                y += _ir[i] * _buffer[idx];
            }

            _position = (_position + 1) % _ir.Length;

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
            Array.Clear(_buffer, 0, _buffer.Length);
            _position = 0;
        }

        public void UpdateIR(float[] newIR)
        {
            if (newIR == null)
                throw new ArgumentNullException(nameof(newIR));

            if (newIR.Length != _ir.Length)
                throw new ArgumentException("New IR must have the same length.", nameof(newIR));

            Array.Copy(newIR, _ir, newIR.Length);
            Reset();
        }
    }
}
