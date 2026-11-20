using System;

namespace HelixAudio.Effects
{
    public abstract class EffectBase
    {
        protected int SampleRate { get; private set; }
        protected int Channels { get; private set; }

        protected EffectBase(int sampleRate, int channels)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("Channel count must be positive.", nameof(channels));

            SampleRate = sampleRate;
            Channels = channels;
        }

        public abstract float[] Process(float[] input);

        public virtual void Reset()
        {
        }

        protected void ValidateInput(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException("Input buffer cannot be empty.", nameof(input));
            if (input.Length % Channels != 0)
                throw new ArgumentException($"Input buffer length must be a multiple of channel count ({Channels}).", nameof(input));
        }

        protected float ClampSample(float sample)
        {
            return sample > 1.0f ? 1.0f : (sample < -1.0f ? -1.0f : sample);
        }

        protected int SamplesPerFrame => Channels;
    }
}
