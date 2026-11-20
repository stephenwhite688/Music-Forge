using System;

namespace HelixAudio.Mixing
{
    public class CrossfadeMixer : MixerBase
    {
        private int _fadeSamples;
        private int _position;

        public int FadeSamples => _fadeSamples;
        public int Position => _position;

        public float Progress
        {
            get
            {
                if (_fadeSamples == 0)
                    return 1.0f;
                return Math.Min(1.0f, (float)_position / _fadeSamples);
            }
        }

        public bool IsComplete => _position >= _fadeSamples;

        public CrossfadeMixer(int sampleRate, int channels, float fadeMs)
            : base(sampleRate, channels)
        {
            if (fadeMs < 0)
                throw new ArgumentException("Fade duration cannot be negative.", nameof(fadeMs));

            _fadeSamples = (int)((fadeMs / 1000.0f) * sampleRate);
            _position = 0;
        }

        public CrossfadeMixer(int sampleRate, int channels, int fadeSamples)
            : base(sampleRate, channels)
        {
            if (fadeSamples < 0)
                throw new ArgumentException("Fade samples cannot be negative.", nameof(fadeSamples));

            _fadeSamples = fadeSamples;
            _position = 0;
        }

        public override float[] Mix(float[] a, float[] b)
        {
            ValidateInput(a);
            ValidateInput(b);

            int maxLength = Math.Max(a.Length, b.Length);
            float[] output = new float[maxLength];

            if (_fadeSamples == 0)
            {
                for (int i = 0; i < maxLength; i++)
                {
                    float sampleA = (i < a.Length) ? a[i] : 0.0f;
                    float sampleB = (i < b.Length) ? b[i] : 0.0f;
                    output[i] = ClampSample((sampleA + sampleB) * 0.5f);
                }
                _position = _fadeSamples;
                return output;
            }

            int startPos = _position;

            for (int i = 0; i < maxLength; i++)
            {
                int currentPos = startPos + i;
                float t = (currentPos >= _fadeSamples) ? 1.0f : (float)currentPos / _fadeSamples;

                float weightA = 1.0f - t;
                float weightB = t;

                float sampleA = (i < a.Length) ? a[i] * weightA : 0.0f;
                float sampleB = (i < b.Length) ? b[i] * weightB : 0.0f;
                output[i] = ClampSample(sampleA + sampleB);
            }

            _position += maxLength / Channels;
            return output;
        }

        public float[] Mix(float[] a, float[] b, int sampleCount)
        {
            ValidateInput(a);
            ValidateInput(b);

            if (sampleCount <= 0)
                return new float[0];

            float[] output = new float[sampleCount];
            int startPos = _position;

            if (_fadeSamples == 0)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    float sampleA = (i < a.Length) ? a[i] : 0.0f;
                    float sampleB = (i < b.Length) ? b[i] : 0.0f;
                    output[i] = ClampSample((sampleA + sampleB) * 0.5f);
                }
                _position = _fadeSamples;
                return output;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                int currentPos = startPos + i;
                float t = (currentPos >= _fadeSamples) ? 1.0f : (float)currentPos / _fadeSamples;

                float weightA = 1.0f - t;
                float weightB = t;

                float sampleA = (i < a.Length) ? a[i] * weightA : 0.0f;
                float sampleB = (i < b.Length) ? b[i] * weightB : 0.0f;
                output[i] = ClampSample(sampleA + sampleB);
            }

            _position += sampleCount / Channels;
            return output;
        }

        public void Reset()
        {
            _position = 0;
        }

        public void ResetTo(int position)
        {
            _position = Math.Max(0, Math.Min(_fadeSamples, position));
        }

        public void SetFadeDuration(int fadeSamples)
        {
            if (fadeSamples < 0)
                throw new ArgumentException("Fade samples cannot be negative.", nameof(fadeSamples));

            _fadeSamples = fadeSamples;
            if (_position > _fadeSamples)
                _position = _fadeSamples;
        }
    }
}
