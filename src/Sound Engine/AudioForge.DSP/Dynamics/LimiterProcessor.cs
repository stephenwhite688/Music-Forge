using System;

namespace AudioForge.DSP.Dynamics
{
    public class LimiterProcessor
    {
        private readonly LimiterEnvelope envelope = new();

        public float CeilingDb { get; set; } = -0.3f;
        public float AttackMs { get; set; } = 1.0f;
        public float ReleaseMs { get; set; } = 50f;

        private readonly int sampleRate;

        public LimiterProcessor(int sampleRate)
        {
            this.sampleRate = sampleRate;
        }

        public void Process(float[] input, float[] output)
        {
            float ceiling = MathF.Pow(10f, CeilingDb / 20f);

            float attack = MathF.Exp(-1f / (0.001f * AttackMs * sampleRate));
            float release = MathF.Exp(-1f / (0.001f * ReleaseMs * sampleRate));

            for (int i = 0; i < input.Length; i++)
            {
                float sample = input[i];
                float level = MathF.Abs(sample);

                float env = envelope.Process(level, attack, release);

                float gain = 1f;

                if (env > ceiling)
                {
                    gain = ceiling / env;
                }

                output[i] = sample * gain;
            }
        }
    }
}
