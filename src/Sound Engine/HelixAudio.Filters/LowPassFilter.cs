using System;

namespace HelixAudio.Filters
{
    /// <summary>
    /// One-pole low-pass filter.
    /// </summary>
    public class LowPassFilter : FilterBase
    {
        private float _cutoffHz;
        private float _coefficient;
        private readonly float[] _prevOutput;

        public float CutoffHz => _cutoffHz;

        public LowPassFilter(int sampleRate, int channels, float cutoffHz)
            : base(sampleRate, channels)
        {
            if (cutoffHz <= 0)
                throw new ArgumentException("Cutoff frequency must be positive.", nameof(cutoffHz));

            _cutoffHz = cutoffHz;
            _prevOutput = new float[channels];
            UpdateCoefficient();
            Reset();
        }

        public void UpdateCoefficient()
        {
            float normalizedFreq = (2.0f * (float)Math.PI * _cutoffHz) / SampleRate;
            _coefficient = (float)Math.Exp(-normalizedFreq);
        }

        public override float[] Process(float[] input)
        {
            ValidateInput(input);

            float[] output = new float[input.Length];
            int channels = Channels;
            float coeff = _coefficient;
            float oneMinusCoeff = 1.0f - coeff;

            float[] prevOut = new float[channels];
            Array.Copy(_prevOutput, prevOut, channels);

            for (int i = 0; i < input.Length; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i + ch;

                    float sample = oneMinusCoeff * input[index] + coeff * prevOut[ch];
                    sample = ClampSample(sample);

                    output[index] = sample;
                    prevOut[ch] = sample;
                }
            }

            Array.Copy(prevOut, _prevOutput, channels);

            return output;
        }

        public override void Reset()
        {
            Array.Clear(_prevOutput, 0, _prevOutput.Length);
        }

        public void SetCutoff(float newCutoffHz)
        {
            if (newCutoffHz <= 0)
                throw new ArgumentException("Cutoff frequency must be positive.", nameof(newCutoffHz));

            if (Math.Abs(_cutoffHz - newCutoffHz) > 0.001f)
            {
                _cutoffHz = newCutoffHz;
                UpdateCoefficient();
            }
        }
    }
}
