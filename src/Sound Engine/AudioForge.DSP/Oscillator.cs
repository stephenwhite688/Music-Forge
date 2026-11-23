using System;

namespace AudioForge.DSP
{
    public enum Waveform
    {
        Sine,
        Square,
        Saw,
        Triangle,
        Noise
    }

    public class Oscillator
    {
        private readonly Random _random;
        private float _frequency;
        private float _phase;
        private float _amplitude;
        private Waveform _waveform;
        private readonly int _sampleRate;
        private float _phaseIncrement;

        public float Frequency
        {
            get => _frequency;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Frequency cannot be negative.", nameof(value));
                _frequency = value;
                UpdatePhaseIncrement();
            }
        }

        public float Phase
        {
            get => _phase;
            set => _phase = value % (TWO_PI);
        }

        public float Amplitude
        {
            get => _amplitude;
            set => _amplitude = Math.Clamp(value, 0.0f, 1.0f);
        }

        public Waveform WaveformType
        {
            get => _waveform;
            set => _waveform = value;
        }

        public Oscillator(int sampleRate)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _random = new Random();
            _frequency = 440.0f;
            _phase = 0.0f;
            _amplitude = 1.0f;
            _waveform = Waveform.Sine;
            UpdatePhaseIncrement();
        }

        public float NextSample()
        {
            float sample = 0.0f;

            switch (_waveform)
            {
                case Waveform.Sine:
                    sample = (float)Math.Sin(_phase);
                    break;

                case Waveform.Square:
                    sample = _phase < PI ? 1.0f : -1.0f;
                    break;

                case Waveform.Saw:
                    sample = -1.0f + (2.0f * _phase / TWO_PI);
                    break;

                case Waveform.Triangle:
                    float normalized = _phase / TWO_PI;
                    sample = 2.0f * (float)Math.Abs(2.0f * (normalized - (float)Math.Floor(normalized + 0.5f))) - 1.0f;
                    break;

                case Waveform.Noise:
                    sample = (float)(_random.NextDouble() * 2.0 - 1.0);
                    break;
            }

            _phase += _phaseIncrement;
            if (_phase >= TWO_PI)
                _phase -= TWO_PI;

            return sample * _amplitude;
        }

        public float[] Generate(int sampleCount)
        {
            if (sampleCount <= 0)
                return new float[0];

            float[] output = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                output[i] = NextSample();
            }
            return output;
        }

        public void SetFrequency(float hz)
        {
            Frequency = hz;
        }

        public void Reset()
        {
            _phase = 0.0f;
        }

        private void UpdatePhaseIncrement()
        {
            _phaseIncrement = TWO_PI * _frequency / _sampleRate;
        }

        private const float PI = (float)Math.PI;
        private const float TWO_PI = 2.0f * PI;
    }
}
