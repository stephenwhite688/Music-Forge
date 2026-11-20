using System;

namespace HelixAudio.Mixing
{
    public class SimpleMixer : MixerBase
    {
        private float _volumeA;
        private float _volumeB;

        public float VolumeA
        {
            get => _volumeA;
            set => _volumeA = Math.Max(0.0f, value);
        }

        public float VolumeB
        {
            get => _volumeB;
            set => _volumeB = Math.Max(0.0f, value);
        }

        public SimpleMixer(int sampleRate, int channels, float volumeA = 1.0f, float volumeB = 1.0f)
            : base(sampleRate, channels)
        {
            _volumeA = Math.Max(0.0f, volumeA);
            _volumeB = Math.Max(0.0f, volumeB);
        }

        public void SetVolumeA(float volume)
        {
            _volumeA = Math.Max(0.0f, volume);
        }

        public void SetVolumeB(float volume)
        {
            _volumeB = Math.Max(0.0f, volume);
        }

        public void SetVolumes(float volumeA, float volumeB)
        {
            _volumeA = Math.Max(0.0f, volumeA);
            _volumeB = Math.Max(0.0f, volumeB);
        }

        public override float[] Mix(float[] a, float[] b)
        {
            ValidateInput(a);
            ValidateInput(b);

            int maxLength = Math.Max(a.Length, b.Length);
            float[] output = new float[maxLength];
            float volA = _volumeA;
            float volB = _volumeB;

            for (int i = 0; i < maxLength; i++)
            {
                float sampleA = (i < a.Length) ? a[i] * volA : 0.0f;
                float sampleB = (i < b.Length) ? b[i] * volB : 0.0f;
                output[i] = ClampSample(sampleA + sampleB);
            }

            return output;
        }

        public float[] Mix(float[] a, float[] b, int sampleCount)
        {
            ValidateInput(a);
            ValidateInput(b);

            if (sampleCount <= 0)
                return new float[0];

            float[] output = new float[sampleCount];
            float volA = _volumeA;
            float volB = _volumeB;

            for (int i = 0; i < sampleCount; i++)
            {
                float sampleA = (i < a.Length) ? a[i] * volA : 0.0f;
                float sampleB = (i < b.Length) ? b[i] * volB : 0.0f;
                output[i] = ClampSample(sampleA + sampleB);
            }

            return output;
        }
    }
}
