using System;

namespace HelixAudio.Effects
{
    /// <summary>
    /// Internal helper class for comb filter used in reverb.
    /// </summary>
    internal class CombFilter
    {
        private readonly float[] _buffer;
        private int _writePos;
        private readonly float _feedback;
        private readonly int _channels;

        public CombFilter(int sampleRate, int channels, int delaySamples, float feedback)
        {
            if (delaySamples <= 0)
                throw new ArgumentException("Delay samples must be positive.", nameof(delaySamples));
            if (feedback < 0 || feedback > 1.0f)
                throw new ArgumentException("Feedback must be between 0 and 1.", nameof(feedback));

            _channels = channels;
            _feedback = feedback;
            _buffer = new float[delaySamples * channels];
            _writePos = 0;
        }

        public float[] Process(float[] input)
        {
            float[] output = new float[input.Length];
            int channels = _channels;
            float feedback = _feedback;
            float[] buffer = _buffer;
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

                    buffer[bufferIndex] = newSample > 1.0f ? 1.0f : (newSample < -1.0f ? -1.0f : newSample);
                    output[index] = delayed;
                }

                writePos += channels;
                if (writePos >= bufferLength)
                    writePos = 0;
            }

            _writePos = writePos;
            return output;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
        }
    }
}
