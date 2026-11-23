using System;
using AudioForge.DSP.GraphicEQ;

namespace AudioForge.DSP.MasterEQ
{
    public class MasterEQProcessor
    {
        private readonly BiquadFilter[] filters;
        private readonly float[] freqs;
        private readonly int sampleRate;

        public MasterEQProcessor(float[] frequencies, int sampleRate)
        {
            this.sampleRate = sampleRate;
            freqs = frequencies;

            filters = new BiquadFilter[freqs.Length];

            for (int i = 0; i < filters.Length; i++)
            {
                // Use Peak filter for graphic EQ bands
                filters[i] = new BiquadFilter(sampleRate, BiquadType.Peak);

                // Default params
                filters[i].SetParams(freqs[i], 1.0f, 0.0f);
            }
        }

        public void SetBandGain(int band, float gainDb)
        {
            float frequency = freqs[band];
            float Q = 1.0f;

            // Use built-in SetParams instead of SetCoefficients
            filters[band].SetParams(frequency, Q, gainDb);
        }

        public void Process(float[] input, float[] output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                float sample = input[i];

                for (int b = 0; b < filters.Length; b++)
                {
                    sample = filters[b].ProcessSample(sample);
                }

                output[i] = sample;
            }
        }
    }
}
