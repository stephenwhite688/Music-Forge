using System;

namespace AudioForge.DSP
{
    public enum BiquadType
    {
        LowPass,
        HighPass,
        BandPass,
        Notch,
        Peak,
        LowShelf,
        HighShelf
    }

    public class BiquadFilter
    {
        private float _a0, _a1, _a2;
        private float _b1, _b2;
        private float _x1, _x2;   // input delay line
        private float _y1, _y2;   // output delay line
        private float _frequency;
        private float _q;
        private float _gainDb;
        private BiquadType _type;
        private readonly int _sampleRate;

        public float Frequency
        {
            get => _frequency;
            set
            {
                _frequency = Math.Max(1.0f, value);
                UpdateCoefficients();
            }
        }

        public float Q
        {
            get => _q;
            set
            {
                _q = Math.Max(0.1f, value);
                UpdateCoefficients();
            }
        }

        public float GainDb
        {
            get => _gainDb;
            set
            {
                _gainDb = value;
                UpdateCoefficients();
            }
        }

        public BiquadType Type
        {
            get => _type;
            set
            {
                _type = value;
                UpdateCoefficients();
            }
        }

        public BiquadFilter(int sampleRate, BiquadType type = BiquadType.LowPass)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _type = type;
            _frequency = 1000.0f;
            _q = 0.707f;
            _gainDb = 0.0f;
            _x1 = 0.0f;
            _x2 = 0.0f;
            _y1 = 0.0f;
            _y2 = 0.0f;
            UpdateCoefficients();
        }

        public void SetParams(float frequency, float q, float gainDb = 0.0f)
        {
            _frequency = Math.Max(1.0f, frequency);
            _q = Math.Max(0.1f, q);
            _gainDb = gainDb;
            UpdateCoefficients();
        }

        public float ProcessSample(float x)
        {
            // Direct Form I: y = b0*x + b1*x1 + b2*x2 - a1*y1 - a2*y2
            // (_a0.._a2 hold the normalised numerator, _b1/_b2 the denominator)
            float y = _a0 * x + _a1 * _x1 + _a2 * _x2 - _b1 * _y1 - _b2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return y;
        }

        public float[] Process(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            float[] output = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = ProcessSample(input[i]);
            }
            return output;
        }

        /// <summary>
        /// Returns the normalised coefficients in Direct-Form-I layout:
        /// numerator (b0,b1,b2), denominator (1,a1,a2). Used for
        /// frequency-response evaluation without running audio through
        /// the filter.
        /// </summary>
        public void GetNormalizedCoefficients(out float b0, out float b1, out float b2, out float a1, out float a2)
        {
            b0 = _a0; b1 = _a1; b2 = _a2;
            a1 = _b1; a2 = _b2;
        }

        /// <summary>
        /// Magnitude response |H(e^jw)| at <paramref name="frequency"/> Hz.
        /// </summary>
        public float MagnitudeResponse(float frequency)
        {
            double w = 2.0 * Math.PI * frequency / _sampleRate;
            double cosW = Math.Cos(w), sinW = Math.Sin(w);
            // H = (b0 + b1 e^-jw + b2 e^-j2w) / (1 + a1 e^-jw + a2 e^-j2w)
            double numRe = _a0 + _a1 * cosW + _a2 * Math.Cos(2 * w);
            double numIm = -_a1 * sinW - _a2 * Math.Sin(2 * w);
            double denRe = 1.0 + _b1 * cosW + _b2 * Math.Cos(2 * w);
            double denIm = -_b1 * sinW - _b2 * Math.Sin(2 * w);
            double mag = Math.Sqrt(numRe * numRe + numIm * numIm)
                       / Math.Sqrt(denRe * denRe + denIm * denIm);
            return (float)mag;
        }

        /// <summary>Magnitude response in dB at <paramref name="frequency"/> Hz.</summary>
        public float MagnitudeResponseDb(float frequency)
        {
            float mag = MagnitudeResponse(frequency);
            return mag > 1e-7f ? 20f * MathF.Log10(mag) : -120f;
        }

        public void Reset()
        {
            _x1 = 0.0f;
            _x2 = 0.0f;
            _y1 = 0.0f;
            _y2 = 0.0f;
        }

        private void UpdateCoefficients()
        {
            float w0 = 2.0f * (float)Math.PI * _frequency / _sampleRate;
            float cosW0 = (float)Math.Cos(w0);
            float sinW0 = (float)Math.Sin(w0);
            float alpha = sinW0 / (2.0f * _q);

            float a0 = 1.0f;
            float a1 = 0.0f;
            float a2 = 0.0f;
            float b0 = 1.0f;
            float b1 = 0.0f;
            float b2 = 0.0f;

            switch (_type)
            {
                case BiquadType.LowPass:
                    b0 = (1.0f - cosW0) / 2.0f;
                    b1 = 1.0f - cosW0;
                    b2 = (1.0f - cosW0) / 2.0f;
                    a0 = 1.0f + alpha;
                    a1 = -2.0f * cosW0;
                    a2 = 1.0f - alpha;
                    break;

                case BiquadType.HighPass:
                    b0 = (1.0f + cosW0) / 2.0f;
                    b1 = -(1.0f + cosW0);
                    b2 = (1.0f + cosW0) / 2.0f;
                    a0 = 1.0f + alpha;
                    a1 = -2.0f * cosW0;
                    a2 = 1.0f - alpha;
                    break;

                case BiquadType.BandPass:
                    b0 = alpha;
                    b1 = 0.0f;
                    b2 = -alpha;
                    a0 = 1.0f + alpha;
                    a1 = -2.0f * cosW0;
                    a2 = 1.0f - alpha;
                    break;

                case BiquadType.Notch:
                    b0 = 1.0f;
                    b1 = -2.0f * cosW0;
                    b2 = 1.0f;
                    a0 = 1.0f + alpha;
                    a1 = -2.0f * cosW0;
                    a2 = 1.0f - alpha;
                    break;

                case BiquadType.Peak:
                    float gain = (float)Math.Pow(10.0, _gainDb / 40.0);
                    b0 = 1.0f + alpha * gain;
                    b1 = -2.0f * cosW0;
                    b2 = 1.0f - alpha * gain;
                    a0 = 1.0f + alpha / gain;
                    a1 = -2.0f * cosW0;
                    a2 = 1.0f - alpha / gain;
                    break;

                case BiquadType.LowShelf:
                    float sqrtGain = (float)Math.Pow(10.0, _gainDb / 40.0);
                    float sqrt2 = (float)Math.Sqrt(2.0);
                    float beta = sqrtGain * sqrt2 * sinW0 / 2.0f;
                    b0 = sqrtGain * ((sqrtGain + 1.0f) + (sqrtGain - 1.0f) * cosW0 + beta);
                    b1 = 2.0f * sqrtGain * ((sqrtGain - 1.0f) + (sqrtGain + 1.0f) * cosW0);
                    b2 = sqrtGain * ((sqrtGain + 1.0f) + (sqrtGain - 1.0f) * cosW0 - beta);
                    a0 = (sqrtGain + 1.0f) - (sqrtGain - 1.0f) * cosW0 + beta;
                    a1 = -2.0f * ((sqrtGain - 1.0f) - (sqrtGain + 1.0f) * cosW0);
                    a2 = (sqrtGain + 1.0f) - (sqrtGain - 1.0f) * cosW0 - beta;
                    break;

                case BiquadType.HighShelf:
                    sqrtGain = (float)Math.Pow(10.0, _gainDb / 40.0);
                    sqrt2 = (float)Math.Sqrt(2.0);
                    beta = sqrtGain * sqrt2 * sinW0 / 2.0f;
                    b0 = sqrtGain * ((sqrtGain + 1.0f) - (sqrtGain - 1.0f) * cosW0 + beta);
                    b1 = -2.0f * sqrtGain * ((sqrtGain - 1.0f) - (sqrtGain + 1.0f) * cosW0);
                    b2 = sqrtGain * ((sqrtGain + 1.0f) - (sqrtGain - 1.0f) * cosW0 - beta);
                    a0 = (sqrtGain + 1.0f) + (sqrtGain - 1.0f) * cosW0 + beta;
                    a1 = 2.0f * ((sqrtGain - 1.0f) + (sqrtGain + 1.0f) * cosW0);
                    a2 = (sqrtGain + 1.0f) + (sqrtGain - 1.0f) * cosW0 - beta;
                    break;
            }

            float invA0 = 1.0f / a0;
            _a0 = b0 * invA0;
            _a1 = b1 * invA0;
            _a2 = b2 * invA0;
            _b1 = a1 * invA0;
            _b2 = a2 * invA0;
        }
    }
}
