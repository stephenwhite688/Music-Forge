using System;

namespace HelixAudio.Core
{
    public abstract class EngineNode : AudioNode
    {
        protected EngineNode(int sampleRate, int channels, string name)
            : base(sampleRate, channels, name)
        {
        }
    }
}
