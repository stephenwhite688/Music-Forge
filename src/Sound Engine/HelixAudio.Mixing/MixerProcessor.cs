using System;
using System.Collections.Generic;

namespace HelixAudio.Mixing
{
    public class MixerProcessor
    {
        private readonly List<MixerChannel> _channels = new();
        private readonly float _panLaw; // e.g. -3 dB

        public MixerProcessor(float panLawDb = -3.0f)
        {
            _panLaw = (float)Math.Pow(10.0, panLawDb / 20.0);
        }

        public void AddChannel(MixerChannel channel)
        {
            _channels.Add(channel);
        }

        public void Process(float[][] inputs, float[] leftOut, float[] rightOut)
        {
            Array.Clear(leftOut, 0, leftOut.Length);
            Array.Clear(rightOut, 0, rightOut.Length);

            bool anySolo = _channels.Exists(c => c.Solo);

            for (int c = 0; c < _channels.Count; c++)
            {
                var ch = _channels[c];
                if (!ch.Enabled)
                    continue;

                if (anySolo && !ch.Solo)
                    continue;

                if (ch.Mute)
                    continue;

                var input = inputs[c];
                float gain = ch.Gain;

                float pan = ch.Pan;
                float leftGain;
                float rightGain;

                // simple equal-power pan law
                float angle = (pan + 1.0f) * 0.5f * MathF.PI / 2.0f;
                leftGain = MathF.Cos(angle) * _panLaw;
                rightGain = MathF.Sin(angle) * _panLaw;

                for (int i = 0; i < input.Length; i++)
                {
                    float sample = input[i] * gain;
                    leftOut[i] += sample * leftGain;
                    rightOut[i] += sample * rightGain;
                }
            }
        }
    }
}
