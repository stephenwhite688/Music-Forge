using System;

namespace HelixAudio.Effects
{
    /// <summary>
    /// Internal helper class for allpass filter used in reverb.
    /// </summary>
    internal class AllpassFilter
    {
        private readonly float[] _buffer;
        private int _writePos;
        private readonly float _gain;
        private readonly int _channels;

        public AllpassFilter(int sampleRate, int channels, int delaySamples, float gain)
        {
            if (delaySamples <= 0)
                throw new ArgumentException("Delay samples must be positive.", nameof(delaySamples));
            if (gain < 0 || gain > 1.0f)
                throw new ArgumentException("Gain must be between 0 and 1.", nameof(gain));

            _channels = channels;
            _gain = gain;
            _buffer = new float[delaySamples * channels];
            _writePos = 0;
        }

        public float[] Process(float[] input)
        {
            float[] output = new float[input.Length];
            int channels = _channels;
            float gain = _gain;
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
                    float newSample = input[index] + delayed * gain;

                    buffer[bufferIndex] = newSample > 1.0f ? 1.0f : (newSample < -1.0f ? -1.0f : newSample);

                    float outputSample = delayed - input[index] * gain;
                    output[index] = outputSample > 1.0f ? 1.0f : (outputSample < -1.0f ? -1.0f : outputSample);
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
