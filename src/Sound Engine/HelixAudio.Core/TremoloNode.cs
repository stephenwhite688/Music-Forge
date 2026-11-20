using System;

namespace HelixAudio.Core
{
    public class TremoloNode
    {
        private float _depth;
        private float _rateHz;
        private float _sampleRate;
        private float _phase;

        public float Depth
        {
            get => _depth;
            set => _depth = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float RateHz
        {
            get => _rateHz;
            set => _rateHz = Math.Max(0.1f, value);
        }

        public TremoloNode(float sampleRate, float depth = 0.5f, float rateHz = 2.0f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _depth = Math.Clamp(depth, 0.0f, 1.0f);
            _rateHz = Math.Max(0.1f, rateHz);
            _phase = 0.0f;
        }

        public void Process(float[] buffer, int frames)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            
            if (frames <= 0)
                return;

            int sampleCount = frames * 2;
            if (buffer.Length < sampleCount)
                throw new ArgumentException("Buffer is too small for the specified frame count.");

            float depth = _depth;
            float phase = _phase;
            float rate = _rateHz / _sampleRate;

            for (int i = 0; i < sampleCount; i++)
            {
                float lfo = (float)Math.Sin(phase);
                float gain = 1.0f - depth * (0.5f + 0.5f * lfo);
                
                float output = buffer[i] * gain;
                buffer[i] = Math.Clamp(output, -1.0f, 1.0f);
                
                phase += rate;
                if (phase > 2.0f * Math.PI)
                    phase -= 2.0f * (float)Math.PI;
            }
            
            _phase = phase;
        }

        public void Reset()
        {
            _phase = 0.0f;
        }
    }
}
