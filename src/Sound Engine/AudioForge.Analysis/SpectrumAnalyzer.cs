using System;
using AudioForge.DSP.Analysis;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Computes a positive-frequency magnitude spectrum from interleaved audio samples.
    /// </summary>
    public class SpectrumAnalyzer
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _fftSize;

        public SpectrumAnalyzer(int sampleRate, int channels, int fftSize = 2048)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
            if (fftSize < 2 || (fftSize & (fftSize - 1)) != 0)
                throw new ArgumentException("FFT size must be a positive power of two.", nameof(fftSize));

            _sampleRate = sampleRate;
            _channels = channels;
            _fftSize = fftSize;
        }

        public int SampleRate => _sampleRate;
        public int Channels => _channels;
        public int FftSize => _fftSize;
        public int BinCount => _fftSize / 2;
        public double BinWidth => (double)_sampleRate / _fftSize;

        public float[] Analyze(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException("Input buffer cannot be empty.", nameof(input));
            if (input.Length % _channels != 0)
                throw new ArgumentException("Input length must be a multiple of the channel count.", nameof(input));

            var real = new float[_fftSize];
            var imaginary = new float[_fftSize];
            var frameCount = input.Length / _channels;
            var framesToCopy = Math.Min(frameCount, _fftSize);
            var sourceOffset = frameCount - framesToCopy;

            for (var frame = 0; frame < framesToCopy; frame++)
            {
                var sum = 0f;
                var inputFrame = sourceOffset + frame;
                for (var channel = 0; channel < _channels; channel++)
                    sum += input[inputFrame * _channels + channel];

                var window = 0.5f - 0.5f * (float)Math.Cos(2d * Math.PI * frame / Math.Max(1, framesToCopy - 1));
                real[frame] = sum / _channels * window;
            }

            FFT.Compute(real, imaginary);

            var magnitudes = new float[BinCount];
            for (var bin = 0; bin < magnitudes.Length; bin++)
            {
                var magnitude = MathF.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]) / _fftSize;
                magnitudes[bin] = bin == 0 || bin == magnitudes.Length - 1 ? magnitude : magnitude * 2f;
            }

            return magnitudes;
        }

        public double GetFrequency(int bin)
        {
            if (bin < 0 || bin >= BinCount)
                throw new ArgumentOutOfRangeException(nameof(bin));
            return bin * BinWidth;
        }
    }
}
