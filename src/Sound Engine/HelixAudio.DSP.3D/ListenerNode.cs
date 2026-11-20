using System;
using HelixAudio.Core;

namespace HelixAudio.DSP._3D
{
    public sealed class ListenerNode : AudioNode
    {
        private Vector3 _position = Vector3.Zero;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _velocity = Vector3.Zero;
        private float _distanceScale = 1f;

        public Vector3 Position
        {
            get => _position;
            set => _position = value;
        }

        public Quaternion Rotation
        {
            get => _rotation;
            set => _rotation = value;
        }

        public Vector3 Velocity
        {
            get => _velocity;
            set => _velocity = value;
        }

        public float DistanceScale
        {
            get => _distanceScale;
            set => _distanceScale = Math.Max(0.001f, value);
        }

        public Vector3 Forward => _rotation.Rotate(Vector3.Forward);
        public Vector3 Up => _rotation.Rotate(Vector3.Up);
        public Vector3 Right => _rotation.Rotate(Vector3.Right);

        // ✔ REQUIRED CONSTRUCTOR (this was missing)
        public ListenerNode(int sampleRate, int channels, string name = "Listener")
            : base(sampleRate, channels, name)
        {
        }

        public override float[] Process(float[] input)
        {
            // ListenerNode does not modify audio; it only tracks spatial state.
            if (input == null || input.Length == 0)
                return input;

            float[] output = new float[input.Length];
            Array.Copy(input, output, input.Length);
            return output;
        }
    }
}
