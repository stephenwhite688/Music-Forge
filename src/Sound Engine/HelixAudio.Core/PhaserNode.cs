using System;

namespace HelixAudio.Core
{
    public class PhaserNode
    {
        private float _depth;
        private float _rateHz;
        private float _feedback;
        private float _mix;
        private float _sampleRate;
        
        private float _phase;
        private float _baseFc;
        private float _depthFc;
        
        private float[] _leftState;
        private float[] _rightState;
        private float _leftFeedback;
        private float _rightFeedback;

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

        public PhaserNode(float sampleRate, float depth = 0.5f, float rateHz = 0.5f,
                          float feedback = 0.5f, float mix = 0.5f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _depth = Math.Clamp(depth, 0.0f, 1.0f);
            _rateHz = Math.Max(0.1f, rateHz);
            _feedback = Math.Clamp(feedback, 0.0f, 0.99f);
            _mix = Math.Clamp(mix, 0.0f, 1.0f);
            
            _phase = 0.0f;
            _baseFc = 300.0f;
            _depthFc = 2000.0f;
            
            _leftState = new float[6];
            _rightState = new float[6];
            _leftFeedback = 0.0f;
            _rightFeedback = 0.0f;
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

            float mix = _mix;
            float oneMinusMix = 1.0f - mix;
            float feedback = _feedback;
            float depth = _depth;
            float phase = _phase;
            float rate = _rateHz / _sampleRate;
            float baseFc = _baseFc;
            float depthFc = _depthFc;
            
            float leftFeedback = _leftFeedback;
            float rightFeedback = _rightFeedback;
            float[] leftState = _leftState;
            float[] rightState = _rightState;

            for (int i = 0; i < sampleCount; i += 2)
            {
                float lfo = (float)Math.Sin(phase);
                float fc = baseFc + depth * depthFc * (0.5f + 0.5f * lfo);
                float omega = 2.0f * (float)Math.PI * fc / _sampleRate;
                float g = (float)Math.Tan(omega / 2.0f);
                
                float inputLeft = buffer[i];
                float processedLeft = ProcessChannel(inputLeft + leftFeedback * feedback, g, leftState);
                leftFeedback = processedLeft;
                
                float lfoRight = (float)Math.Sin(phase + Math.PI);
                float fcRight = baseFc + depth * depthFc * (0.5f + 0.5f * lfoRight);
                float omegaRight = 2.0f * (float)Math.PI * fcRight / _sampleRate;
                float gRight = (float)Math.Tan(omegaRight / 2.0f);
                
                float inputRight = buffer[i + 1];
                float processedRight = ProcessChannel(inputRight + rightFeedback * feedback, gRight, rightState);
                rightFeedback = processedRight;
                
                float outputLeft = inputLeft * oneMinusMix + processedLeft * mix;
                float outputRight = inputRight * oneMinusMix + processedRight * mix;
                
                buffer[i] = Math.Clamp(outputLeft, -1.0f, 1.0f);
                buffer[i + 1] = Math.Clamp(outputRight, -1.0f, 1.0f);
                
                phase += rate;
                if (phase > 2.0f * Math.PI)
                    phase -= 2.0f * (float)Math.PI;
            }
            
            _phase = phase;
            _leftFeedback = leftFeedback;
            _rightFeedback = rightFeedback;
        }

        private float ProcessChannel(float input, float g, float[] state)
        {
            float output = input;
            for (int i = 0; i < 6; i++)
            {
                float prevOutput = state[i];
                output = input + g * (prevOutput - input);
                state[i] = output;
                input = output;
            }
            return output;
        }

        public void Reset()
        {
            _phase = 0.0f;
            _leftFeedback = 0.0f;
            _rightFeedback = 0.0f;
            Array.Clear(_leftState, 0, _leftState.Length);
            Array.Clear(_rightState, 0, _rightState.Length);
        }
    }
}
