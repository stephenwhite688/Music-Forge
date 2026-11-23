using System;

namespace AudioForge.Effects
{
    /// <summary>
    /// Internal helper: allpass filter used by <see cref="ReverbEffect"/>.
    /// </summary>
    internal sealed class AllpassFilter
    {
        private readonly float[] _buffer;
        private int _writePos;
        private readonly float _gain;
        private readonly int _channels;

        public AllpassFilter(int channels, int delaySamples, float gain)
        {
            if (delaySamples <= 0)
                throw new ArgumentException("Delay samples must be positive.", nameof(delaySamples));
            if (gain < 0 || gain > 1.0f)
                throw new ArgumentException("Gain must be between 0 and 1.", nameof(gain));

            _channels = channels;
            _gain = gain;
            _buffer = new float[delaySamples * channels];
        }

        /// <summary>
        /// Processes interleaved audio. <paramref name="output"/> may alias
        /// <paramref name="input"/> (in-place safe).
        /// </summary>
        public void Process(Span<float> input, Span<float> output)
        {
            int channels = _channels;
            float gain = _gain;
            var buffer = _buffer;
            int writePos = _writePos;
            int bufferLength = buffer.Length;

            for (int i = 0; i < input.Length; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i + ch;
                    int bufferIndex = writePos + ch;

                    float delayed = buffer[bufferIndex];
                    float newSample = input[index] + delayed * gain;

                    buffer[bufferIndex] = Math.Clamp(newSample, -1f, 1f);
                    output[index] = Math.Clamp(delayed - input[index] * gain, -1f, 1f);
                }

                writePos += channels;
                if (writePos >= bufferLength)
                    writePos = 0;
            }

            _writePos = writePos;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
        }
    }
}
