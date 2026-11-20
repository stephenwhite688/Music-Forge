using System;
using System.Collections.Generic;

namespace HelixAudio.Core
{
    /// <summary>
    /// Audio mixer node that combines multiple input nodes with volume control.
    /// </summary>
    public class AudioMixer : AudioNode
    {
        private readonly List<IAudioNode> _inputs = new();
        private readonly Dictionary<IAudioNode, float> _volumes = new();
        private int _sampleRate;
        private int _channels;

        /// <summary>
        /// Initializes a new instance of the AudioMixer class.
        /// </summary>
        /// <param name="sampleRate">The audio sample rate in Hz.</param>
        /// <param name="channels">The number of audio channels.</param>
        /// <param name="name">The node name (default: "Mixer").</param>
        public AudioMixer(int sampleRate, int channels, string name = "Mixer")
            : base(sampleRate, channels, name)
        {
            _sampleRate = sampleRate;
            _channels = channels;
        }

        /// <summary>
        /// Adds an input node to the mixer with optional volume control.
        /// </summary>
        /// <param name="node">The audio node to add.</param>
        /// <param name="volume">The volume gain (default: 1.0).</param>
        public void AddInput(IAudioNode node, float volume = 1.0f)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!_inputs.Contains(node))
            {
                _inputs.Add(node);
                _volumes[node] = volume;
            }
        }

        /// <summary>
        /// Removes an input node from the mixer.
        /// </summary>
        /// <param name="node">The audio node to remove.</param>
        public void RemoveInput(IAudioNode node)
        {
            if (node == null) return;
            _inputs.Remove(node);
            _volumes.Remove(node);
        }

        /// <summary>
        /// Sets the volume gain for a specific input node.
        /// </summary>
        /// <param name="node">The audio node whose volume to set.</param>
        /// <param name="volume">The new volume gain.</param>
        public void SetVolume(IAudioNode node, float volume)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (_volumes.ContainsKey(node))
                _volumes[node] = volume;
        }

        /// <inheritdoc/>
        public override float[] Process(float[] buffer)
        {
            if (_inputs.Count == 0) return buffer;

            float[] output = new float[buffer.Length];
            Array.Copy(buffer, output, buffer.Length);

            foreach (var node in _inputs)
            {
                try
                {
                    float[] processed = node.Process(buffer);
                    float volume = _volumes.TryGetValue(node, out float v) ? v : 1.0f;
                    for (int i = 0; i < output.Length && i < processed.Length; i++)
                        output[i] += processed[i] * volume;
                }
                catch { }
            }

            for (int i = 0; i < output.Length; i++)
                output[i] = Math.Clamp(output[i], -1.0f, 1.0f);

            return output;
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            foreach (var node in _inputs)
                try { node.Reset(); } catch { }
        }
    }
}