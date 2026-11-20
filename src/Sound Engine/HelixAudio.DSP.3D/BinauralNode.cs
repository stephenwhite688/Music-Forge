using HelixAudio.Core;
using System;

namespace HelixAudio.DSP._3D
{
    public sealed class BinauralNode : AudioNode
    {
        // Simple head‑related parameters you can expand later if needed
        private float _azimuth = 0f;
        private float _elevation = 0f;
        private float _distance = 1f;

        public bool IsBypassed { get; set; }

        public float Azimuth
        {
            get => _azimuth;
            set => _azimuth = value % 360f;
        }

        public float Elevation
        {
            get => _elevation;
            set => _elevation = Math.Clamp(value, -90f, 90f);
        }

        public float Distance
        {
            get => _distance;
            set => _distance = Math.Max(0.01f, value);
        }

        public BinauralNode(int sampleRate, int channels, string name = "Binaural")
            : base(sampleRate, channels, name)
        {
        }

        public override void Prepare(AudioFormat format)
        {
            base.Prepare(format);
            // Any binaural-specific setup can go here later
        }

        public override float[] Process(float[] buffer)
        {
            // For now, this is a transparent pass‑through.
            // It satisfies AudioNode’s abstract contract and compiles cleanly.
            if (buffer == null || buffer.Length == 0)
                return buffer;

            var output = new float[buffer.Length];
            Array.Copy(buffer, output, buffer.Length);
            return output;
        }

        public override void Reset()
        {
            // Reset any internal state if you add it later
        }
    }
}
