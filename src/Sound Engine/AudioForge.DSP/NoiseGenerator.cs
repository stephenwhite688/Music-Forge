using System;

namespace AudioForge.DSP
{
    public enum NoiseType
    {
        White,
        Pink,
        Brown
    }

    public class NoiseGenerator
    {
        private readonly Random _random;
        private NoiseType _type;
        private float _pinkState;
        private float _brownState;
        private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;

        public NoiseType Type
        {
            get => _type;
            set => _type = value;
        }

        public NoiseGenerator(NoiseType type = NoiseType.White)
        {
            _random = new Random();
            _type = type;
            _pinkState = 0.0f;
            _brownState = 0.0f;
            Reset();
        }

        public float NextSample()
        {
            float sample = 0.0f;

            switch (_type)
            {
                case NoiseType.White:
                    sample = (float)(_random.NextDouble() * 2.0 - 1.0);
                    break;

                case NoiseType.Pink:
                    sample = GeneratePink();
                    break;

                case NoiseType.Brown:
                    sample = GenerateBrown();
                    break;
            }

            return sample;
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

        private float GeneratePink()
        {
            float white = (float)(_random.NextDouble() * 2.0 - 1.0);

            _b0 = 0.99886f * _b0 + white * 0.0555179f;
            _b1 = 0.99332f * _b1 + white * 0.0750759f;
            _b2 = 0.96900f * _b2 + white * 0.1538520f;
            _b3 = 0.86650f * _b3 + white * 0.3104856f;
            _b4 = 0.55000f * _b4 + white * 0.5329522f;
            _b5 = -0.7616f * _b5 - white * 0.0168980f;

            float pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f;
            _b6 = white * 0.115926f;

            return Math.Clamp(pink * 0.11f, -1.0f, 1.0f);
        }

        private float GenerateBrown()
        {
            float white = (float)(_random.NextDouble() * 2.0 - 1.0);
            float brown = _brownState + white * 0.1f;

            brown = Math.Clamp(brown, -1.0f, 1.0f);
            _brownState = brown;

            return brown;
        }

        public void Reset()
        {
            _pinkState = 0.0f;
            _brownState = 0.0f;
            _b0 = 0.0f;
            _b1 = 0.0f;
            _b2 = 0.0f;
            _b3 = 0.0f;
            _b4 = 0.0f;
            _b5 = 0.0f;
            _b6 = 0.0f;
        }
    }
}


