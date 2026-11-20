using System;
using System.Collections.Generic;

namespace HelixAudio.Core
{
    public class AudioPipeline
    {
        private readonly List<IAudioNode> _nodes = new();

        public void AddNode(IAudioNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _nodes.Add(node);
        }

        public float[] Execute(float[] buffer)
        {
            float[] result = buffer;
            foreach (var node in _nodes)
            {
                try { result = node.Process(result); }
                catch { }
            }
            return result;
        }
    }
}
