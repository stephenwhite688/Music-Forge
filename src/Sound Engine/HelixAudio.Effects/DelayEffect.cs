using System;

namespace HelixAudio.Effects
{
    public class DelayEffect : EffectBase
    {
        private float _delayMs;
        private float _feedback;
        private float _mix;
        private float[] _buffer;
        private int _writePos;
        private int _delaySamples;

        public float DelayMs
        {
            get => _delayMs;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Delay time cannot be negative.", nameof(value));
                _delayMs = value;
                UpdateDelaySamples();
            }
        }

        public float Feedback
        {
            get => _feedback;
            set => _feedback = Math.Clamp(value, 0.0f, 0.99f);
        }

        public float Mix
        {
            get => _mix;
            set => _mix = Math.Clamp(value, 0.0f, 1.0f);
        }

        public DelayEffect(int sampleRate, int channels, float delayMs = 500.0f,
                           float feedback = 0.5f, float mix = 0.5f)
            : base(sampleRate, channels)
        {
            if (delayMs < 0)
                throw new ArgumentException("Delay time cannot be negative.", nameof(delayMs));

            _delayMs = delayMs;
            _feedback = Math.Clamp(feedback, 0.0f, 0.99f);
            _mix = Math.Clamp(mix, 0.0f, 1.0f);

            int maxDelaySamples = (int)((delayMs / 1000.0f) * sampleRate) + 1;
            _buffer = new float[maxDelaySamples * channels];
            _writePos = 0;

            UpdateDelaySamples();
        }

        public override float[] Process(float[] input)
        {
            ValidateInput(input);

            float[] output = new float[input.Length];
            int channels = Channels;
            float feedback = _feedback;
            float mix = _mix;
            float oneMinusMix = 1.0f - mix;
            float[] buffer = _buffer;
            int writePos = _writePos;
            int bufferLength = buffer.Length;

            for (int i = 0; i < input.Length; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i + ch;

                    float delayed = buffer[writePos + ch];
                    float newSample = input[index] + delayed * feedback;
                    buffer[writePos + ch] = ClampSample(newSample);

                    float outputSample = input[index] * oneMinusMix + delayed * mix;
                    output[index] = ClampSample(outputSample);
                }

                writePos += channels;
                if (writePos >= bufferLength)
                    writePos = 0;
            }

            _writePos = writePos;
            return output;
        }

        public override void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
        }

        private void UpdateDelaySamples()
        {
            _delaySamples = (int)((_delayMs / 1000.0f) * SampleRate);

            int maxSamples = _buffer.Length / Channels;
            if (_delaySamples > maxSamples)
            {
                _buffer = new float[(_delaySamples + 1) * Channels];
                Reset();
            }
        }
    }
}
