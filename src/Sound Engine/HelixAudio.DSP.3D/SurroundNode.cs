using HelixAudio.Core;
using System;

namespace HelixAudio.DSP._3D
{
    public sealed class SurroundNode : AudioNode
    {
        private SurroundLayout _layout = SurroundLayout.Surround5_1;
        private float _centerLevel = 0f;
        private float _surroundLevel = 0f;
        private float _lfeLevel = 0f;

        public bool IsBypassed { get; set; }

        public SurroundLayout Layout
        {
            get => _layout;
            set => _layout = value;
        }

        public float CenterLevel
        {
            get => _centerLevel;
            set => _centerLevel = Math.Clamp(value, -12f, 12f);
        }

        public float SurroundLevel
        {
            get => _surroundLevel;
            set => _surroundLevel = Math.Clamp(value, -12f, 12f);
        }

        public float LfeLevel
        {
            get => _lfeLevel;
            set => _lfeLevel = Math.Clamp(value, -12f, 12f);
        }

        public SurroundNode(int sampleRate, int channels, string name = "Surround")
            : base(sampleRate, channels, name)
        {
        }

        public override void Prepare(AudioFormat format)
        {
            base.Prepare(format);
        }

        // ✔ REQUIRED override — full surround logic preserved
        public override float[] Process(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return buffer;

            int inChannels = Format.Channels;
            int frameCount = buffer.Length / inChannels;

            // Determine output channel count based on layout
            int outChannels = _layout switch
            {
                SurroundLayout.Surround5_1 => 6,
                SurroundLayout.Surround7_1 => 8,
                SurroundLayout.DolbyAtmos => 8,
                _ => 6
            };

            float[] output = new float[frameCount * outChannels];

            if (IsBypassed)
            {
                // Expand stereo → surround by simple duplication
                for (int f = 0; f < frameCount; f++)
                {
                    float left = buffer[f * inChannels];
                    float right = buffer[f * inChannels + 1];

                    for (int c = 0; c < outChannels; c++)
                        output[f * outChannels + c] = (c % 2 == 0) ? left : right;
                }

                return output;
            }

            // Gains
            float centerGain = MathF.Pow(10f, _centerLevel / 20f);
            float surroundGain = MathF.Pow(10f, _surroundLevel / 20f);
            float lfeGain = MathF.Pow(10f, _lfeLevel / 20f);

            for (int f = 0; f < frameCount; f++)
            {
                float left = buffer[f * inChannels];
                float right = buffer[f * inChannels + 1];

                float lfe = (left + right) * 0.5f * lfeGain;
                float center = (left + right) * 0.5f * centerGain;
                float surroundLeft = (left - right) * 0.5f * surroundGain;
                float surroundRight = (right - left) * 0.5f * surroundGain;

                if (_layout == SurroundLayout.Surround5_1)
                {
                    output[f * 6] = left;
                    output[f * 6 + 1] = right;
                    output[f * 6 + 2] = center;
                    output[f * 6 + 3] = lfe;
                    output[f * 6 + 4] = surroundLeft;
                    output[f * 6 + 5] = surroundRight;
                }
                else if (_layout == SurroundLayout.Surround7_1 || _layout == SurroundLayout.DolbyAtmos)
                {
                    output[f * 8] = left;
                    output[f * 8 + 1] = right;
                    output[f * 8 + 2] = center;
                    output[f * 8 + 3] = lfe;
                    output[f * 8 + 4] = surroundLeft;
                    output[f * 8 + 5] = surroundRight;
                    output[f * 8 + 6] = surroundLeft * 0.5f;
                    output[f * 8 + 7] = surroundRight * 0.5f;
                }
            }

            return output;
        }

        public override void Reset() { }
    }
}
