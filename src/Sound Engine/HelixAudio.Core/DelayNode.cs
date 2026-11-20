using System;

namespace HelixAudio.Core
{
    public class DelayNode
    {
        private readonly float[] _buffer;
        private int _writeIndex;
        private int _readIndex;
        private float _delaySamples;
        private float _sampleRate;

        private float _delayMs;
        private float _feedback;
        private float _mix;

        public float DelayMs
        {
            get => _delayMs;
            set
            {
                _delayMs = Math.Max(0.0f, value);
                UpdateDelaySamples();
            }
        }

        public float Feedback
        {
            get => _feedback;
            set => _feedback = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float Mix
        {
            get => _mix;
            set => _mix = Math.Clamp(value, 0.0f, 1.0f);
        }

        public DelayNode(float sampleRate, float maxDelayMs, float delayMs = 500.0f, 
                         float feedback = 0.5f, float mix = 0.5f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            
            if (maxDelayMs <= 0)
                throw new ArgumentException("Maximum delay must be positive.", nameof(maxDelayMs));

            _sampleRate = sampleRate;
            _delayMs = Math.Max(0.0f, delayMs);
            _feedback = Math.Clamp(feedback, 0.0f, 1.0f);
            _mix = Math.Clamp(mix, 0.0f, 1.0f);

            int maxDelaySamples = (int)Math.Ceiling((maxDelayMs / 1000.0f) * _sampleRate);
            _buffer = new float[maxDelaySamples * 2];
            _writeIndex = 0;
            _readIndex = 0;

            UpdateDelaySamples();
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

            if (_buffer.Length == 0 || _delaySamples < 1.0f)
                return;

            int delaySamplesInt = (int)_delaySamples;
            float delayFraction = _delaySamples - delaySamplesInt;

            int bufferSize = _buffer.Length;

            for (int i = 0; i < sampleCount; i++)
            {
                int readPos = _readIndex + i;
                if (readPos >= bufferSize)
                    readPos -= bufferSize;

                float delayedSample = _buffer[readPos];

                if (delayFraction > 0.001f && delaySamplesInt < bufferSize / 2)
                {
                    int nextReadPos = readPos + 1;
                    if (nextReadPos >= bufferSize)
                        nextReadPos = 0;
                    float nextSample = _buffer[nextReadPos];
                    delayedSample += (nextSample - delayedSample) * delayFraction;
                }

                float inputSample = buffer[i];
                float outputSample = inputSample * (1.0f - _mix) + delayedSample * _mix;

                outputSample = Math.Clamp(outputSample, -1.0f, 1.0f);
                buffer[i] = outputSample;

                float newDelayed = inputSample + delayedSample * _feedback;
                newDelayed = Math.Clamp(newDelayed, -1.0f, 1.0f);

                int writePos = _writeIndex + i;
                if (writePos >= bufferSize)
                    writePos -= bufferSize;
                _buffer[writePos] = newDelayed;
            }

            _writeIndex += sampleCount;
            _writeIndex %= bufferSize;
            _readIndex += sampleCount;
            _readIndex %= bufferSize;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writeIndex = 0;
            _readIndex = 0;
            UpdateDelaySamples();
        }

        private void UpdateDelaySamples()
        {
            _delaySamples = (_delayMs / 1000.0f) * _sampleRate;
            
            int maxSamples = _buffer.Length / 2;
            if (_delaySamples > maxSamples)
                _delaySamples = maxSamples;
        }
    }
}
