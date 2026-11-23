using System;

namespace AudioForge.Effects
{
    /// <summary>
    /// Internal helper: feedback comb filter used by <see cref="ReverbEffect"/>.
    /// Output is the delayed tap; the delay line accumulates input + feedback.
    /// </summary>
    internal sealed class CombFilter
    {
        private readonly float[] _buffer;
        private int _writePos;
        private readonly float _feedback;
        private readonly int _channels;

        public CombFilter(int channels, int delaySamples, float feedback)
        {
            if (delaySamples <= 0)
                throw new ArgumentException("Delay samples must be positive.", nameof(delaySamples));
            if (feedback < 0 || feedback > 1.0f)
                throw new ArgumentException("Feedback must be between 0 and 1.", nameof(feedback));

            _channels = channels;
            _feedback = feedback;
            _buffer = new float[delaySamples * channels];
        }

        /// <summary>
        /// Processes interleaved audio. <paramref name="output"/> may alias
        /// <paramref name="input"/> (in-place safe).
        /// </summary>
        public void Process(Span<float> input, Span<float> output)
        {
            int channels = _channels;
            float feedback = _feedback;
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
                    float newSample = input[index] + delayed * feedback;

                    buffer[bufferIndex] = Math.Clamp(newSample, -1f, 1f);
                    output[index] = delayed;
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
