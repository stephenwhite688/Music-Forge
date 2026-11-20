using System;

namespace HelixAudio.Filters
{
    /// <summary>
    /// One-pole high-pass filter.
    /// </summary>
    public class HighPassFilter : FilterBase
    {
        private float _cutoffHz;
        private float _a;
        private float _b;
        private readonly float[] _prevInput;
        private readonly float[] _prevOutput;

        public float CutoffHz => _cutoffHz;

        public HighPassFilter(int sampleRate, int channels, float cutoffHz)
            : base(sampleRate, channels)
        {
            if (cutoffHz <= 0)
                throw new ArgumentException("Cutoff frequency must be positive.", nameof(cutoffHz));

            _cutoffHz = cutoffHz;
            _prevInput = new float[channels];
            _prevOutput = new float[channels];
            UpdateCoefficients();
            Reset();
        }

        public void UpdateCoefficients()
        {
            float rc = 1.0f / (2.0f * (float)Math.PI * _cutoffHz);
            float dt = 1.0f / SampleRate;
            float alpha = rc / (rc + dt);

            _a = alpha;
            _b = alpha;
        }

        public override float[] Process(float[] input)
        {
            ValidateInput(input);

            float[] output = new float[input.Length];
            int channels = Channels;
            float a = _a;
            float b = _b;

            float[] prevIn = new float[channels];
            float[] prevOut = new float[channels];
            Array.Copy(_prevInput, prevIn, channels);
            Array.Copy(_prevOutput, prevOut, channels);

            for (int i = 0; i < input.Length; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i + ch;

                    float sample = a * prevOut[ch] + b * (input[index] - prevIn[ch]);
                    sample = ClampSample(sample);

                    output[index] = sample;
                    prevIn[ch] = input[index];
                    prevOut[ch] = sample;
                }
            }

            Array.Copy(prevIn, _prevInput, channels);
            Array.Copy(prevOut, _prevOutput, channels);

            return output;
        }

        public override void Reset()
        {
            Array.Clear(_prevInput, 0, _prevInput.Length);
            Array.Clear(_prevOutput, 0, _prevOutput.Length);
        }

        public void SetCutoff(float newCutoffHz)
        {
            if (newCutoffHz <= 0)
                throw new ArgumentException("Cutoff frequency must be positive.", nameof(newCutoffHz));

            if (Math.Abs(_cutoffHz - newCutoffHz) > 0.001f)
            {
                _cutoffHz = newCutoffHz;
                UpdateCoefficients();
            }
        }
    }
}
