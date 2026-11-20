using System;

namespace HelixAudio.Core
{
    public class VibratoNode
    {
        private float _depthMs;
        private float _rateHz;
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

        public VibratoNode(float sampleRate, float depthMs = 5.0f, float rateHz = 2.0f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _depthMs = Math.Max(0.0f, depthMs);
            _rateHz = Math.Max(0.1f, rateHz);
            
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

            float depthSamples = _depthSamples;
            float phase = _phase;
            float rate = _rateHz / _sampleRate;
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
                
                _buffers[0][writeIndex] = inputLeft;
                _buffers[1][writeIndex] = inputRight;
                
                buffer[i] = Math.Clamp(delayedLeft, -1.0f, 1.0f);
                buffer[i + 1] = Math.Clamp(delayedRight, -1.0f, 1.0f);
                
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

        private float ReadInterpolated(float[] buffer, int writeIndex, float readPos, int bufferSize)
        {
            float pos = writeIndex - readPos;
            if (pos < 0)
                pos += bufferSize;
            
            int index = (int)pos;
            float frac = pos - index;
            
            int nextIndex = index + 1;
            if (nextIndex >= bufferSize)
                nextIndex = 0;
            
            float sample1 = buffer[index];
            float sample2 = buffer[nextIndex];
            
            return sample1 + (sample2 - sample1) * frac;
        }

        public void Reset()
        {
            Array.Clear(_buffers[0], 0, _bufferSize);
            Array.Clear(_buffers[1], 0, _bufferSize);
            _writeIndex = 0;
            _phase = 0.0f;
        }
    }
}
