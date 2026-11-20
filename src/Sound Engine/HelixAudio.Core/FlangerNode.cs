using System;

namespace HelixAudio.Core
{
    public class FlangerNode
    {
        private float _depthMs;
        private float _rateHz;
        private float _feedback;
        private float _mix;
        private float _sampleRate;

        private readonly float[][] _buffers;
        private readonly int _bufferSize;
        private int _writeIndex;
        private float _phase;
        private float _depthSamples;

        public float DepthMs
        {
            get => _depthMs;
            set
            {
                _depthMs = Math.Max(0.0f, value);
                _depthSamples = (_depthMs / 1000.0f) * _sampleRate;
            }
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

        public FlangerNode(
            float sampleRate,
            float depthMs = 5.0f,
            float rateHz = 0.5f,
            float feedback = 0.5f,
            float mix = 0.5f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _depthMs = Math.Max(0.0f, depthMs);
            _rateHz = Math.Max(0.1f, rateHz);
            _feedback = Math.Clamp(feedback, 0.0f, 0.99f);
            _mix = Math.Clamp(mix, 0.0f, 1.0f);

            _depthSamples = (_depthMs / 1000.0f) * _sampleRate;

            _bufferSize = (int)((20.0f / 1000.0f) * _sampleRate) + 2;
            _buffers = new float[2][];
            _buffers[0] = new float[_bufferSize];
            _buffers[1] = new float[_bufferSize];
            _writeIndex = 0;
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

            float mix = _mix;
            float oneMinusMix = 1.0f - mix;
            float feedback = _feedback;
            float depthSamples = _depthSamples;
            float rate = _rateHz / _sampleRate;
            float phase = _phase;
            int writeIndex = _writeIndex;
            int bufferSize = _bufferSize;

            for (int i = 0; i < sampleCount; i += 2)
            {
                float inputLeft = buffer[i];
                float delayLeft = depthSamples * (1.0f + (float)Math.Sin(phase)) / 2.0f;
                float delayedLeft = ReadInterpolated(_buffers[0], writeIndex, delayLeft, bufferSize);

                float inputRight = buffer[i + 1];
                float delayRight = depthSamples * (1.0f + (float)Math.Sin(phase + Math.PI)) / 2.0f;
                float delayedRight = ReadInterpolated(_buffers[1], writeIndex, delayRight, bufferSize);

                float newLeft = inputLeft + delayedLeft * feedback;
                float newRight = inputRight + delayedRight * feedback;
                newLeft = Math.Clamp(newLeft, -1.0f, 1.0f);
                newRight = Math.Clamp(newRight, -1.0f, 1.0f);
                _buffers[0][writeIndex] = newLeft;
                _buffers[1][writeIndex] = newRight;

                float outputLeft = inputLeft * oneMinusMix + delayedLeft * mix;
                float outputRight = inputRight * oneMinusMix + delayedRight * mix;

                buffer[i] = Math.Clamp(outputLeft, -1.0f, 1.0f);
                buffer[i + 1] = Math.Clamp(outputRight, -1.0f, 1.0f);

                phase += rate;
                if (phase > 2.0f * Math.PI)
                    phase -= 2.0f * (float)Math.PI;

                writeIndex++;
                if (writeIndex >= bufferSize)
                    writeIndex = 0;
            }

            _phase = phase;
            _writeIndex = writeIndex;
        }

        private float ReadInterpolated(float[] buffer, int writeIndex, float delaySamples, int bufferSize)
        {
            float readPos = writeIndex - delaySamples;
            while (readPos < 0)
                readPos += bufferSize;

            int idx1 = (int)readPos;
            int idx2 = (idx1 + 1) % bufferSize;
            float frac = readPos - idx1;

            float s1 = buffer[idx1];
            float s2 = buffer[idx2];

            return s1 * (1.0f - frac) + s2 * frac;
        }

        public void Reset()
        {
            Array.Clear(_buffers[0], 0, _buffers[0].Length);
            Array.Clear(_buffers[1], 0, _buffers[1].Length);
            _writeIndex = 0;
            _phase = 0.0f;
        }
    }
}
