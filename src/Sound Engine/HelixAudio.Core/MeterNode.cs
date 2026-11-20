using System;

namespace HelixAudio.Core
{
    public class MeterNode
    {
        private float _peak;
        private float _rms;

        public float Peak => _peak;
        public float Rms => _rms;

        public MeterNode()
        {
            Reset();
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

            float peak = 0.0f;
            double sumSquares = 0.0;

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = buffer[i];
                float absSample = Math.Abs(sample);
                
                if (absSample > peak)
                    peak = absSample;
                
                sumSquares += sample * sample;
            }

            _peak = peak;

            if (sampleCount > 0)
            {
                _rms = (float)Math.Sqrt(sumSquares / sampleCount);
            }
            else
            {
                _rms = 0.0f;
            }
        }

        public void Reset()
        {
            _peak = 0.0f;
            _rms = 0.0f;
        }
    }
}
