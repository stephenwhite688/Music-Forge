using System;

namespace HelixAudio.Filters
{
    /// <summary>
    /// Simple band-pass filter using low-pass and high-pass cascade.
    /// </summary>
    public class BandPassFilter : FilterBase
    {
        private readonly LowPassFilter _lowPass;
        private readonly HighPassFilter _highPass;

        public float LowCut => _highPass.CutoffHz;
        public float HighCut => _lowPass.CutoffHz;

        public BandPassFilter(int sampleRate, int channels, float lowCut, float highCut)
            : base(sampleRate, channels)
        {
            if (lowCut <= 0)
                throw new ArgumentException("Lower cutoff frequency must be positive.", nameof(lowCut));
            if (highCut <= 0)
                throw new ArgumentException("Higher cutoff frequency must be positive.", nameof(highCut));
            if (lowCut >= highCut)
                throw new ArgumentException("Lower cutoff must be less than higher cutoff.", nameof(lowCut));

            _highPass = new HighPassFilter(sampleRate, channels, lowCut);
            _lowPass = new LowPassFilter(sampleRate, channels, highCut);
        }

        public override float[] Process(float[] input)
        {
            ValidateInput(input);

            float[] output = _highPass.Process(input);
            output = _lowPass.Process(output);

            return output;
        }

        public override void Reset()
        {
            _highPass.Reset();
            _lowPass.Reset();
        }

        public void UpdateBand(float lowCut, float highCut)
        {
            if (lowCut <= 0)
                throw new ArgumentException("Lower cutoff frequency must be positive.", nameof(lowCut));
            if (highCut <= 0)
                throw new ArgumentException("Higher cutoff frequency must be positive.", nameof(highCut));
            if (lowCut >= highCut)
                throw new ArgumentException("Lower cutoff must be less than higher cutoff.", nameof(lowCut));

            _highPass.SetCutoff(lowCut);
            _lowPass.SetCutoff(highCut);
        }

        public float GetCenterFrequency()
        {
            return (float)Math.Sqrt(LowCut * HighCut);
        }

        public float GetBandwidth()
        {
            return HighCut - LowCut;
        }
    }
}
