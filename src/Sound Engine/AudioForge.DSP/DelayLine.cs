using System;

namespace AudioForge.DSP
{
    /// <summary>
    /// Delay line with fractional delay support via linear interpolation.
    /// </summary>
    public class DelayLine
    {
        private readonly float[] _buffer;
        private int _writeIndex;
        private readonly int _maxDelaySamples;

        /// <summary>
        /// Gets the maximum delay in samples.
        /// </summary>
        public int MaxDelaySamples => _maxDelaySamples;

        /// <summary>
        /// Initializes a new instance of the DelayLine class.
        /// </summary>
        /// <param name="maxDelaySamples">Maximum delay in samples.</param>
        public DelayLine(int maxDelaySamples)
        {
            if (maxDelaySamples <= 0)
                throw new ArgumentException("Maximum delay must be positive.", nameof(maxDelaySamples));

            _maxDelaySamples = maxDelaySamples;
            _buffer = new float[maxDelaySamples + 1]; // +1 for interpolation safety
            _writeIndex = 0;
        }

        /// <summary>
        /// Writes a sample to the delay line.
        /// </summary>
        /// <param name="sample">Sample to write.</param>
        public void Write(float sample)
        {
            _buffer[_writeIndex] = sample;
            _writeIndex = (_writeIndex + 1) % _buffer.Length;
        }

        /// <summary>
        /// Reads a sample from the delay line with linear interpolation.
        /// </summary>
        /// <param name="delaySamples">Delay in samples (can be fractional).</param>
        /// <returns>Interpolated sample value.</returns>
        public float ReadInterpolated(float delaySamples)
        {
            if (delaySamples < 0)
                throw new ArgumentException("Delay cannot be negative.", nameof(delaySamples));

            if (delaySamples > _maxDelaySamples)
                delaySamples = _maxDelaySamples;

            // Calculate read position
            float readPos = _writeIndex - delaySamples;
            
            // Handle wrap-around
            if (readPos < 0)
                readPos += _buffer.Length;

            int index = (int)readPos;
            float frac = readPos - index;

            // Get the two samples for interpolation
            int nextIndex = index + 1;
            if (nextIndex >= _buffer.Length)
                nextIndex = 0;

            float sample1 = _buffer[index];
            float sample2 = _buffer[nextIndex];

            // Linear interpolation
            return sample1 + (sample2 - sample1) * frac;
        }

        /// <summary>
        /// Resets the delay line to its initial state.
        /// </summary>
        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writeIndex = 0;
        }

        /// <summary>
        /// Gets the current delay line buffer.
        /// </summary>
        /// <returns>Internal buffer array.</returns>
        public float[] GetBuffer()
        {
            return _buffer;
        }
    }
}
