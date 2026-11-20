using HelixAudio.Core;
using System;

namespace HelixAudio.Effects
{
    /// <summary>
    /// Feedback delay as an <see cref="IAudioNode"/>: mono-compatible
    /// (delay line is per-channel), with feedback and dry/wet mix.
    /// Processes in place - no per-block allocation.
    /// </summary>
    public class DelayEffect : AudioNode
    {
        private float _delayMs;
        private float _feedback;
        private float _mix;
        private float[] _buffer = Array.Empty<float>();
        private int _writePos;
        private int _delaySamples;

        public float DelayMs
        {
            get => _delayMs;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Delay time cannot be negative.", nameof(value));
                _delayMs = value;
                UpdateDelaySamples();
            }
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

        public DelayEffect(int sampleRate, int channels, float delayMs = 500.0f,
                           float feedback = 0.5f, float mix = 0.5f, string name = "Delay")
            : base(sampleRate, channels, name)
        {
            if (delayMs < 0)
                throw new ArgumentException("Delay time cannot be negative.", nameof(delayMs));

            _delayMs = delayMs;
            _feedback = Math.Clamp(feedback, 0.0f, 0.99f);
            _mix = Math.Clamp(mix, 0.0f, 1.0f);

            EnsureDelayBuffer();
            UpdateDelaySamples();
        }

        private int ChannelCount => Math.Max(1, Format?.Channels ?? Channels);

        private void EnsureDelayBuffer()
        {
            int needed = ((int)((_delayMs / 1000f) * SampleRate) + 1) * ChannelCount;
            // Grow when too small; shrink only when the request drops below
            // half the current size so small DelayMs tweaks don't thrash.
            if (_buffer.Length < needed || _buffer.Length > needed * 2)
            {
                _buffer = new float[needed];
                _writePos = 0;
            }
        }

        private void UpdateDelaySamples()
        {
            _delaySamples = (int)((_delayMs / 1000f) * SampleRate);
            EnsureDelayBuffer();
        }

        public override void Prepare(AudioFormat format)
        {
            base.Prepare(format);
            EnsureDelayBuffer();
        }

        public override void Process(Span<float> buffer)
        {
            if (buffer.Length == 0 || _buffer.Length == 0) return;

            int channels = ChannelCount;
            if (buffer.Length % channels != 0) return;

            float feedback = _feedback;
            float mix = _mix;
            float oneMinusMix = 1f - mix;
            var delayLine = _buffer;
            int writePos = _writePos;
            int bufferLength = delayLine.Length;

            for (int i = 0; i < buffer.Length; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int index = i + ch;
                    int bufferIndex = writePos + ch;

                    float input = buffer[index];
                    float delayed = delayLine[bufferIndex];
                    delayLine[bufferIndex] = Math.Clamp(input + delayed * feedback, -1f, 1f);
                    buffer[index] = Math.Clamp(input * oneMinusMix + delayed * mix, -1f, 1f);
                }

                writePos += channels;
                if (writePos >= bufferLength)
                    writePos = 0;
            }

            _writePos = writePos;
        }

        public override void Reset()
        {
            if (_buffer.Length > 0)
                Array.Clear(_buffer, 0, _buffer.Length);
            _writePos = 0;
        }
    }
}
