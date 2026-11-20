using System;
using HelixAudio.Effects;

namespace HelixAudio.Engine
{
    public class EffectNode : EngineNode
    {
        private readonly EffectBase _effect;

        public EffectBase Effect => _effect;

        public EffectNode(int sampleRate, int channels, EffectBase effect, string name = null)
            : base(sampleRate, channels, NodeType.Effect, name)
        {
            _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        public override float[] Process(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException("Buffer cannot be empty.", nameof(input));
            if (input.Length % Channels != 0)
                throw new ArgumentException($"Buffer length must be a multiple of channel count ({Channels}).", nameof(input));

            return _effect.Process(input);
        }

        public override void Reset()
        {
            _effect.Reset();
        }
    }
}
