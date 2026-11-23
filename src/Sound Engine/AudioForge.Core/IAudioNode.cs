using System;

namespace AudioForge.Core
{
    /// <summary>
    /// Interface representing an audio processing node that can be added to an audio graph.
    /// Nodes process audio buffers and can have metadata (name, format) and lifecycle methods.
    /// </summary>
    public interface IAudioNode
    {
        /// <summary>
        /// Gets the name of this audio node.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the audio format associated with this node (sample rate, channels, sample type).
        /// </summary>
        AudioFormat Format { get; }

        /// <summary>
        /// Prepares the node for processing with the specified format.
        /// Called once when the engine initializes the node's format.
        /// </summary>
        /// <param name="format">The audio format to prepare for.</param>
        void Prepare(AudioFormat format);

        /// <summary>
        /// Processes an interleaved audio buffer in place.
        /// The buffer contains <c>frames * Format.Channels</c> samples.
        /// Implementations must not allocate per block on the real-time path.
        /// </summary>
        /// <param name="buffer">The interleaved audio buffer to process in place.</param>
        void Process(Span<float> buffer);

        /// <summary>
        /// Resets the node state to initial values.
        /// Called before processing begins to ensure clean state.
        /// </summary>
        void Reset();
    }
}