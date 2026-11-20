using System;

namespace HelixAudio.Core
{
    /// <summary>
    /// Creates a chorus effect by combining the original audio with slightly detuned, time-delayed copies.
    /// Produces a rich, swirling sound commonly used in synthesizers and vocal processing.
    /// </summary>
    public class ChorusNode
    {
        private float _depthMs;
        private float _rateHz;
        private float _mix;
        private float _sampleRate;
        
        private readonly float[][] _buffers;
        private readonly int _bufferSize;
        private int _writeIndex;
        private float _phase;
        private float _baseDelaySamples;
        private float _depthSamples;

        /// <summary>
        /// Gets or sets the modulation depth in milliseconds. Controls how far the delay varies from the base delay.
        /// Higher values create a wider, more sweeping chorus effect.
        /// </summary>
        public float DepthMs
        {
            get => _depthMs;
            set
            {
                _depthMs = Math.Max(0.0f, value);
                _depthSamples = (_depthMs / 1000.0f) * _sampleRate;
            }
        }

        /// <summary>
        /// Gets or sets the modulation rate in Hz. Controls how fast the delay time oscillates.
        /// Lower values produce a slower, more subtle chorus effect (typically 0.1-5 Hz).
        /// </summary>
        public float RateHz
        {
            get => _rateHz;
            set => _rateHz = Math.Max(0.1f, value);
        }

        /// <summary>
        /// Gets or sets the mix ratio between dry and wet signals. 0.0 means no chorus effect (100% dry),
        /// 1.0 means only the chorus signal is heard (100% wet). Typical values are 0.3-0.7.
        /// </summary>
        public float Mix
        {
            get => _mix;
            set => _mix = Math.Clamp(value, 0.0f, 1.0f);
        }

        /// <summary>
        /// Initializes a new instance of the ChorusNode class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="depthMs">Modulation depth in milliseconds. Default is 5.0 ms.</param>
        /// <param name="rateHz">Modulation rate in Hz. Default is 1.0 Hz.</param>
        /// <param name="mix">Mix ratio between dry and wet signals (0.0 to 1.0). Default is 0.5.</param>
        public ChorusNode(float sampleRate, float depthMs = 5.0f, float rateHz = 1.0f, float mix = 0.5f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _depthMs = Math.Max(0.0f, depthMs);
            _rateHz = Math.Max(0.1f, rateHz);
            _mix = Math.Clamp(mix, 0.0f, 1.0f);
            
            _depthSamples = (_depthMs / 1000.0f) * _sampleRate;
            _baseDelaySamples = 5.0f;
            
            _bufferSize = (int)((50.0f / 1000.0f) * _sampleRate) + 2;
            _buffers = new float[2][];
            _buffers[0] = new float[_bufferSize];
            _buffers[1] = new float[_bufferSize];
            _writeIndex = 0;
            _phase = 0.0f;
        }

        /// <summary>
        /// Processes the input stereo buffer, applying the chorus effect.
        /// Uses two delay lines with LFO-modulated delays to create the chorus sound.
        /// </summary>
        /// <param name="buffer">Stereo audio buffer (left channel first, then right).</param>
        /// <param name="frames">Number of frames (stereo pairs) to process.</param>
        public void Process(float[] buffer, int frames)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            
            if (frames <= 0)
                return;

            int sampleCount = frames * 2;
            if (buffer.Length < sampleCount)
                throw new ArgumentException("Buffer is too small for the specified frame count.", nameof(buffer));

            float mix = _mix;
            float oneMinusMix = 1.0f - mix;
            float depthSamples = _depthSamples;
            float baseDelaySamples = _baseDelaySamples;
            float rate = _rateHz / _sampleRate;
            float phase = _phase;
            int writeIndex = _writeIndex;
            int bufferSize = _bufferSize;

            for (int i = 0; i < sampleCount; i += 2)
            {
                float inputLeft = buffer[i];
                float delayLeft = baseDelaySamples + depthSamples * (float)Math.Sin(phase);
                float delayedLeft = ReadInterpolated(_buffers[0], writeIndex, delayLeft, bufferSize);
                
                float inputRight = buffer[i + 1];
                float delayRight = baseDelaySamples + depthSamples * (float)Math.Sin(phase + Math.PI / 2);
                float delayedRight = ReadInterpolated(_buffers[1], writeIndex, delayRight, bufferSize);
                
                _buffers[0][writeIndex] = inputLeft;
                _buffers[1][writeIndex] = inputRight;
                
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

        /// <summary>
        /// Reads a sample from the delay buffer using linear interpolation.
        /// Handles wrap-around for circular buffer implementation.
        /// </summary>
        /// <param name="buffer">The delay buffer to read from.</param>
        /// <param name="writeIndex">Current write position in the buffer.</param>
        /// <param name="readPos">Position to read (relative to write index, can be fractional).</param>
        /// <param name="bufferSize">Total size of the buffer.</param>
        /// <returns>The interpolated sample value at the requested position.</returns>
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

        /// <summary>
        /// Resets the chorus node to initial state, clearing delay buffers and resetting phase.
        /// </summary>
        public void Reset()
        {
            Array.Clear(_buffers[0], 0, _bufferSize);
            Array.Clear(_buffers[1], 0, _bufferSize);
            _writeIndex = 0;
            _phase = 0.0f;
        }
    }
}