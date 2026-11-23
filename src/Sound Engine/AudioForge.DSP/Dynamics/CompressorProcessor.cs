using System;

namespace AudioForge.DSP.Dynamics
{
    public class CompressorProcessor
    {
        private readonly CompressorEnvelope envelope = new();

        public float ThresholdDb { get; set; } = -12f;
        public float Ratio { get; set; } = 4f;
        public float AttackMs { get; set; } = 10f;
        public float ReleaseMs { get; set; } = 120f;
        public float MakeupGainDb { get; set; } = 0f;

        private readonly int sampleRate;

        public CompressorProcessor(int sampleRate)
        {
            this.sampleRate = sampleRate;
        }

        public void Process(float[] input, float[] output)
        {
            float threshold = MathF.Pow(10f, ThresholdDb / 20f);
            float makeup = MathF.Pow(10f, MakeupGainDb / 20f);

            float attack = MathF.Exp(-1f / (0.001f * AttackMs * sampleRate));
            float release = MathF.Exp(-1f / (0.001f * ReleaseMs * sampleRate));

            for (int i = 0; i < input.Length; i++)
            {
                float sample = input[i];
                float level = MathF.Abs(sample);

                float env = envelope.Process(level, attack, release);

                float gain = 1f;

                if (env > threshold)
                {
                    float over = env / threshold;
                    float compressed = MathF.Pow(over, (1f - 1f / Ratio));
                    gain = compressed / over;
                }

                output[i] = sample * gain * makeup;
            }
        }
    }
}
