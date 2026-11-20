using System.Collections.Generic;

namespace HelixAudio.Core
{
    /// <summary>
    /// Simple audio engine that chains a list of IAudioNode processors in sequence.
    /// Each node receives the output of the previous node and returns its processed result.
    /// </summary>
    public class AudioEngine
    {
        private readonly List<IAudioNode> _nodes = new();

        /// <summary>
        /// Adds an audio node to the processing chain.
        /// </summary>
        /// <param name="node">The node to add to the end of the chain.</param>
        public void AddNode(IAudioNode node) => _nodes.Add(node);

        /// <summary>
        /// Processes an audio buffer by passing it through all nodes in the chain.
        /// </summary>
        /// <param name="input">The input audio buffer to process.</param>
        /// <returns>The processed audio buffer after all nodes have been applied.</returns>
        public float[] Process(float[] input)
        {
            float[] buffer = input;
            foreach (var node in _nodes)
                buffer = node.Process(buffer);
            return buffer;
        }
    }
}