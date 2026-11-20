using System;

namespace HelixAudio.Mixing
{
    public class PanMixer : MixerBase
    {
        private float _pan;

        public float Pan
        {
            get => _pan;
            set => _pan = Math.Clamp(value, -1.0f, 1.0f);
        }

        public PanMixer(int sampleRate, float initialPan = 0.0f)
            : base(sampleRate, 2)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _pan = Math.Clamp(initialPan, -1.0f, 1.0f);
        }

        public void SetPan(float pan)
        {
            _pan = Math.Clamp(pan, -1.0f, 1.0f);
        }

        public override float[] Mix(float[] a, float[] b)
        {
            ValidateInput(a);
            ValidateInput(b);

            if (a.Length % 2 != 0 || b.Length % 2 != 0)
                throw new ArgumentException("Both inputs must be stereo (even number of samples).");

            int maxLength = Math.Max(a.Length, b.Length);
            float[] output = new float[maxLength];
            float pan = _pan;

            float leftGain = (float)Math.Sqrt(0.5f * (1.0f - pan));
            float rightGain = (float)Math.Sqrt(0.5f * (1.0f + pan));

            for (int i = 0; i < maxLength; i += 2)
            {
                float sampleAL = (i < a.Length) ? a[i] : 0.0f;
                float sampleBL = (i < b.Length) ? b[i] : 0.0f;
                float mixedL = (sampleAL + sampleBL) * leftGain;
                output[i] = ClampSample(mixedL);

                if (i + 1 < maxLength)
                {
                    float sampleAR = (i + 1 < a.Length) ? a[i + 1] : 0.0f;
                    float sampleBR = (i + 1 < b.Length) ? b[i + 1] : 0.0f;
                    float mixedR = (sampleAR + sampleBR) * rightGain;
                    output[i + 1] = ClampSample(mixedR);
                }
            }

            return output;
        }

        public float[] Mix(float[] a, float[] b, int sampleCount)
        {
            ValidateInput(a);
            ValidateInput(b);

            if (sampleCount <= 0 || sampleCount % 2 != 0)
                throw new ArgumentException("Sample count must be positive and even for stereo.");

            float[] output = new float[sampleCount];
            float pan = _pan;

            float leftGain = (float)Math.Sqrt(0.5f * (1.0f - pan));
            float rightGain = (float)Math.Sqrt(0.5f * (1.0f + pan));

            for (int i = 0; i < sampleCount; i += 2)
            {
                float sampleAL = (i < a.Length) ? a[i] : 0.0f;
                float sampleBL = (i < b.Length) ? b[i] : 0.0f;
                float mixedL = (sampleAL + sampleBL) * leftGain;
                output[i] = ClampSample(mixedL);

                if (i + 1 < sampleCount)
                {
                    float sampleAR = (i + 1 < a.Length) ? a[i + 1] : 0.0f;
                    float sampleBR = (i + 1 < b.Length) ? b[i + 1] : 0.0f;
                    float mixedR = (sampleAR + sampleBR) * rightGain;
                    output[i + 1] = ClampSample(mixedR);
                }
            }

            return output;
        }
    }
}
