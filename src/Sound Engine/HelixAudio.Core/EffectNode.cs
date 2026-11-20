using System;

namespace HelixAudio.Core
{
    public abstract class EffectNode : AudioNode
    {
        protected EffectNode(int sampleRate, int channels, string name)
            : base(sampleRate, channels, name)
        {
        }

        public sealed override float[] Process(float[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            ProcessBuffer(buffer);
            return buffer;
        }

        protected abstract void ProcessBuffer(float[] buffer);
    }
}
