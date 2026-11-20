using System;

namespace HelixAudio.Core
{
    public class LimiterNode
    {
        private float _ceilingDb;
        private float _ceilingLinear;

        public float CeilingDb
        {
            get => _ceilingDb;
            set
            {
                _ceilingDb = value;
                _ceilingLinear = (float)Math.Pow(10.0, _ceilingDb / 20.0);
                _ceilingLinear = Math.Clamp(_ceilingLinear, 0.0001f, 1.0f);
            }
        }

        public LimiterNode(float ceilingDb = -0.1f)
        {
            _ceilingDb = ceilingDb;
            _ceilingLinear = (float)Math.Pow(10.0, _ceilingDb / 20.0);
            _ceilingLinear = Math.Clamp(_ceilingLinear, 0.0001f, 1.0f);
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

            float ceiling = _ceilingLinear;

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = buffer[i];
                float absSample = Math.Abs(sample);
                
                if (absSample > ceiling)
                {
                    sample = Math.Sign(sample) * ceiling;
                }
                
                sample = Math.Clamp(sample, -1.0f, 1.0f);
                buffer[i] = sample;
            }
        }

        public void Reset()
        {
        }
    }
}
