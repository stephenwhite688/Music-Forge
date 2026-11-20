using System;
using System.Collections.Generic;
using HelixAudio.Mixing;

namespace HelixAudio.Engine
{
    public class MixerNode : EngineNode
    {
        private readonly MixerBase _mixer;
        private readonly List<float[]> _pendingInputs;

        public MixerBase Mixer => _mixer;

        public MixerNode(int sampleRate, int channels, MixerBase mixer, string name = null)
            : base(sampleRate, channels, NodeType.Mixer, name)
        {
            _mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
            _pendingInputs = new List<float[]>();
        }

        public void AddInput(float[] buffer)
        {
            if (buffer != null && buffer.Length > 0)
            {
                _pendingInputs.Add(buffer);
            }
        }

        public override float[] Process(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException("Buffer cannot be empty.", nameof(input));
            if (input.Length % Channels != 0)
                throw new ArgumentException($"Buffer length must be a multiple of channel count ({Channels}).", nameof(input));

            if (input != null && input.Length > 0)
            {
                _pendingInputs.Add(input);
            }

            if (_pendingInputs.Count == 0)
                return new float[0];

            if (_pendingInputs.Count == 1)
            {
                var result = _pendingInputs[0];
                _pendingInputs.Clear();
                return result;
            }

            float[] mixed = _pendingInputs[0];
            for (int i = 1; i < _pendingInputs.Count; i++)
            {
                mixed = _mixer.Mix(mixed, _pendingInputs[i]);
            }

            _pendingInputs.Clear();
            return mixed;
        }

        public override void Reset()
        {
            _pendingInputs.Clear();
        }
    }
}
