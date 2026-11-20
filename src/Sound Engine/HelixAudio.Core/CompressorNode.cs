using System;

namespace HelixAudio.Core
{
    public class CompressorNode
    {
        private float _thresholdDb;
        private float _ratio;
        private float _attackMs;
        private float _releaseMs;
        private float _sampleRate;
        
        private float _thresholdLinear;
        private float _attackCoeff;
        private float _releaseCoeff;
        private float _leftEnv;
        private float _rightEnv;
        private float _leftGain;
        private float _rightGain;

        public float ThresholdDb
        {
            get => _thresholdDb;
            set
            {
                _thresholdDb = value;
                _thresholdLinear = (float)Math.Pow(10.0, _thresholdDb / 20.0);
            }
        }

        public float Ratio
        {
            get => _ratio;
            set => _ratio = Math.Max(1.0f, value);
        }

        public float AttackMs
        {
            get => _attackMs;
            set
            {
                _attackMs = Math.Max(0.1f, value);
                UpdateAttackCoeff();
            }
        }

        public float ReleaseMs
        {
            get => _releaseMs;
            set
            {
                _releaseMs = Math.Max(0.1f, value);
                UpdateReleaseCoeff();
            }
        }

        public CompressorNode(float sampleRate, float thresholdDb = -20.0f, float ratio = 4.0f,
                              float attackMs = 10.0f, float releaseMs = 100.0f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _thresholdDb = thresholdDb;
            _thresholdLinear = (float)Math.Pow(10.0, _thresholdDb / 20.0);
            _ratio = Math.Max(1.0f, ratio);
            _attackMs = Math.Max(0.1f, attackMs);
            _releaseMs = Math.Max(0.1f, releaseMs);
            
            _leftEnv = 0.0f;
            _rightEnv = 0.0f;
            _leftGain = 1.0f;
            _rightGain = 1.0f;
            
            UpdateAttackCoeff();
            UpdateReleaseCoeff();
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

            float threshold = _thresholdLinear;
            float ratioMinusOne = _ratio - 1.0f;
            float attackCoeff = _attackCoeff;
            float releaseCoeff = _releaseCoeff;
            
            float leftEnv = _leftEnv;
            float rightEnv = _rightEnv;
            float leftGain = _leftGain;
            float rightGain = _rightGain;

            for (int i = 0; i < sampleCount; i += 2)
            {
                float inputLeft = buffer[i];
                float absLeft = Math.Abs(inputLeft);
                
                float envDeltaLeft = absLeft - leftEnv;
                if (envDeltaLeft > 0)
                    leftEnv += attackCoeff * envDeltaLeft;
                else
                    leftEnv += releaseCoeff * envDeltaLeft;
                
                if (leftEnv > threshold)
                {
                    float gainReduction = (float)Math.Pow(leftEnv / threshold, -ratioMinusOne);
                    leftGain = Math.Min(leftGain, gainReduction);
                }
                else
                {
                    leftGain = 1.0f;
                }
                
                float outputLeft = inputLeft * leftGain;
                outputLeft = Math.Clamp(outputLeft, -1.0f, 1.0f);
                buffer[i] = outputLeft;

                float inputRight = buffer[i + 1];
                float absRight = Math.Abs(inputRight);
                
                float envDeltaRight = absRight - rightEnv;
                if (envDeltaRight > 0)
                    rightEnv += attackCoeff * envDeltaRight;
                else
                    rightEnv += releaseCoeff * envDeltaRight;
                
                if (rightEnv > threshold)
                {
                    float gainReduction = (float)Math.Pow(rightEnv / threshold, -ratioMinusOne);
                    rightGain = Math.Min(rightGain, gainReduction);
                }
                else
                {
                    rightGain = 1.0f;
                }
                
                float outputRight = inputRight * rightGain;
                outputRight = Math.Clamp(outputRight, -1.0f, 1.0f);
                buffer[i + 1] = outputRight;
            }

            _leftEnv = leftEnv;
            _rightEnv = rightEnv;
            _leftGain = leftGain;
            _rightGain = rightGain;
        }

        public void Reset()
        {
            _leftEnv = 0.0f;
            _rightEnv = 0.0f;
            _leftGain = 1.0f;
            _rightGain = 1.0f;
        }

        private void UpdateAttackCoeff()
        {
            _attackCoeff = 1.0f - (float)Math.Exp(-2.0f / (_attackMs * _sampleRate / 1000.0f));
        }

        private void UpdateReleaseCoeff()
        {
            _releaseCoeff = 1.0f - (float)Math.Exp(-2.0f / (_releaseMs * _sampleRate / 1000.0f));
        }
    }
}
