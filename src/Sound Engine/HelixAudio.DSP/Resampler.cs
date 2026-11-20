using System;

namespace HelixAudio.DSP
{
    public enum ResampleMode
    {
        Linear,
        Sinc
    }

    public class Resampler
    {
        private readonly int _inputRate;
        private readonly int _outputRate;
        private readonly ResampleMode _mode;
        private float _phase;
        private float[] _lastInput;

        public Resampler(int inputRate, int outputRate, ResampleMode mode = ResampleMode.Linear)
        {
            if (inputRate <= 0 || outputRate <= 0)
                throw new ArgumentException("Sample rates must be positive.");

            _inputRate = inputRate;
            _outputRate = outputRate;
            _mode = mode;
            _phase = 0.0f;
            _lastInput = new float[1];
        }

        public float[] Process(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (input.Length == 0)
                return new float[0];

            if (_inputRate == _outputRate)
            {
                float[] outputBuffer = new float[input.Length];
                Array.Copy(input, outputBuffer, input.Length);
                return outputBuffer;
            }

            float ratio = (float)_outputRate / _inputRate;
            int outputLength = (int)(input.Length * ratio);
            float[] resampledBuffer = new float[outputLength];

            if (_mode == ResampleMode.Linear)
                ResampleLinear(input, resampledBuffer, ratio);
            else
                ResampleSinc(input, resampledBuffer, ratio);

            return resampledBuffer;
        }

        private void ResampleLinear(float[] input, float[] buffer, float ratio)
        {
            float phase = _phase;

            for (int i = 0; i < buffer.Length; i++)
            {
                float pos = i / ratio + phase;
                int idx = (int)pos;
                float frac = pos - idx;

                if (idx >= input.Length - 1)
                {
                    buffer[i] = input[input.Length - 1];
                    continue;
                }

                buffer[i] = input[idx] + (input[idx + 1] - input[idx]) * frac;
            }

            _phase = (buffer.Length / ratio + phase) - input.Length;
            if (_phase < 0) _phase = 0;
        }

        private void ResampleSinc(float[] input, float[] buffer, float ratio)
        {
            int kernelSize = 16;

            for (int i = 0; i < buffer.Length; i++)
            {
                float pos = i / ratio;
                int idx = (int)pos;
                float frac = pos - idx;

                float sum = 0.0f;
                float sumWeights = 0.0f;

                for (int k = -kernelSize / 2; k < kernelSize / 2; k++)
                {
                    int sampleIdx = idx + k;
                    if (sampleIdx < 0 || sampleIdx >= input.Length)
                        continue;

                    float x = frac - k;
                    float sinc = (float)Math.Sin(Math.PI * x) / (float)(Math.PI * x);
                    if (float.IsNaN(sinc) || float.IsInfinity(sinc))
                        sinc = 1.0f;

                    float window = 0.5f * (1.0f - (float)Math.Cos(2.0f * Math.PI * (k + kernelSize / 2) / kernelSize));
                    float weight = sinc * window;

                    sum += input[sampleIdx] * weight;
                    sumWeights += weight;
                }

                buffer[i] = sumWeights > 0.0f ? sum / sumWeights : 0.0f;
            }
        }

        public void Reset()
        {
            _phase = 0.0f;
            Array.Clear(_lastInput, 0, _lastInput.Length);
        }
    }
}
