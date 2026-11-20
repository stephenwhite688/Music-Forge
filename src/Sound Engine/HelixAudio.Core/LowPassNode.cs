using System;

namespace HelixAudio.Core
{
    public class LowPassNode
    {
        private float _cutoffHz;
        private float _sampleRate;
        private float _coefficient;
        private float _leftState;
        private float _rightState;

        public float CutoffHz
        {
            get => _cutoffHz;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Cutoff frequency must be positive.", nameof(value));
                
                _cutoffHz = value;
                UpdateCoefficient();
            }
        }

        public LowPassNode(float sampleRate, float cutoffHz)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            
            if (cutoffHz <= 0)
                throw new ArgumentException("Cutoff frequency must be positive.", nameof(cutoffHz));

            _sampleRate = sampleRate;
            _cutoffHz = cutoffHz;
            _leftState = 0.0f;
            _rightState = 0.0f;
            UpdateCoefficient();
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

            float x = _coefficient;
            float oneMinusX = 1.0f - x;
            float leftState = _leftState;
            float rightState = _rightState;

            for (int i = 0; i < sampleCount; i += 2)
            {
                float inputLeft = buffer[i];
                float outputLeft = oneMinusX * inputLeft + x * leftState;
                outputLeft = Math.Clamp(outputLeft, -1.0f, 1.0f);
                buffer[i] = outputLeft;
                leftState = outputLeft;

                float inputRight = buffer[i + 1];
                float outputRight = oneMinusX * inputRight + x * rightState;
                outputRight = Math.Clamp(outputRight, -1.0f, 1.0f);
                buffer[i + 1] = outputRight;
                rightState = outputRight;
            }

            _leftState = leftState;
            _rightState = rightState;
        }

        public void Reset()
        {
            _leftState = 0.0f;
            _rightState = 0.0f;
            UpdateCoefficient();
        }

        private void UpdateCoefficient()
        {
            float normalizedFreq = (2.0f * (float)Math.PI * _cutoffHz) / _sampleRate;
            _coefficient = (float)Math.Exp(-normalizedFreq);
        }
    }
}
