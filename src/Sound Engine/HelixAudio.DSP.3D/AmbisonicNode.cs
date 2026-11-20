using HelixAudio.Core;
using System;
using System.Diagnostics;

namespace HelixAudio.DSP._3D
{
    public sealed class AmbisonicNode : AudioNode
    {
        private AmbisonicOrder _order = AmbisonicOrder.First;
        private float _azimuth = 0f;
        private float _elevation = 0f;
        private float _distance = 1f;

        public bool IsBypassed { get; set; }

        public AmbisonicOrder Order
        {
            get => _order;
            set => _order = value;
        }

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

        public AmbisonicNode(int sampleRate, int channels, string name = "Ambisonic")
            : base(sampleRate, channels, name)
        {
        }

        private int ExpectedChannels(AmbisonicOrder order)
        {
            switch (order)
            {
                case AmbisonicOrder.First:
                    return 4;
                case AmbisonicOrder.Second:
                    return 9;
                case AmbisonicOrder.Third:
                    return 16;
                default:
                    return Channels;
            }
        }

        public override void Prepare(AudioFormat format)
        {
            base.Prepare(format);

            try
            {
                int expected = ExpectedChannels(_order);

                if (Channels != expected)
                {
                    Debug.WriteLine(
                        $"[AmbisonicNode] Channel mismatch: got {Channels}, expected {expected}. Bypassing.",
                        "Ambisonic");

                    IsBypassed = true;
                }

                if (format.SampleRate <= 0)
                {
                    Debug.WriteLine(
                        $"[AmbisonicNode] Invalid sample rate {format.SampleRate}. Bypassing.",
                        "Ambisonic");

                    IsBypassed = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AmbisonicNode] Prepare error: {ex}", "Ambisonic");
                IsBypassed = true;
            }
        }

        public override float[] Process(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return buffer;

            if (IsBypassed)
                return buffer;

            try
            {
                if (Channels <= 0 || buffer.Length % Channels != 0)
                {
                    Debug.WriteLine(
                        $"[AmbisonicNode] Invalid buffer length {buffer.Length} for {Channels} channels. Bypassing.",
                        "Ambisonic");

                    return buffer;
                }

                // Safe no-op for now
                return buffer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AmbisonicNode] Process error: {ex}", "Ambisonic");
                return buffer;
            }
        }

        public override void Reset()
        {
            _azimuth = 0f;
            _elevation = 0f;
            _distance = 1f;
            IsBypassed = false;
        }
    }
}
