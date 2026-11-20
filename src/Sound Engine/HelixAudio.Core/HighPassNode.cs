using System;

namespace HelixAudio.Core
{
    public class HighPassNode
    {
        private float _cutoffHz;
        private float _sampleRate;
        private float _coefficient;
        private float _leftPrevInput;
        private float _leftPrevOutput;
        private float _rightPrevInput;
        private float _rightPrevOutput;

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

        public HighPassNode(float sampleRate, float cutoffHz)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            
            if (cutoffHz <= 0)
                throw new ArgumentException("Cutoff frequency must be positive.", nameof(cutoffHz));

            _sampleRate = sampleRate;
            _cutoffHz = cutoffHz;
            _leftPrevInput = 0.0f;
            _leftPrevOutput = 0.0f;
            _rightPrevInput = 0.0f;
            _rightPrevOutput = 0.0f;
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
            float leftPrevInput = _leftPrevInput;
            float leftPrevOutput = _leftPrevOutput;
            float rightPrevInput = _rightPrevInput;
            float rightPrevOutput = _rightPrevOutput;

            for (int i = 0; i < sampleCount; i += 2)
            {
                float inputLeft = buffer[i];
                float outputLeft = x * (leftPrevOutput + inputLeft - leftPrevInput);
                outputLeft = Math.Clamp(outputLeft, -1.0f, 1.0f);
                buffer[i] = outputLeft;
                leftPrevInput = inputLeft;
                leftPrevOutput = outputLeft;

                float inputRight = buffer[i + 1];
                float outputRight = x * (rightPrevOutput + inputRight - rightPrevInput);
                outputRight = Math.Clamp(outputRight, -1.0f, 1.0f);
                buffer[i + 1] = outputRight;
                rightPrevInput = inputRight;
                rightPrevOutput = outputRight;
            }

            _leftPrevInput = leftPrevInput;
            _leftPrevOutput = leftPrevOutput;
            _rightPrevInput = rightPrevInput;
            _rightPrevOutput = rightPrevOutput;
        }

        public void Reset()
        {
            _leftPrevInput = 0.0f;
            _leftPrevOutput = 0.0f;
            _rightPrevInput = 0.0f;
            _rightPrevOutput = 0.0f;
            UpdateCoefficient();
        }

        private void UpdateCoefficient()
        {
            float normalizedFreq = (2.0f * (float)Math.PI * _cutoffHz) / _sampleRate;
            _coefficient = (float)Math.Exp(-normalizedFreq);
        }
    }
}
