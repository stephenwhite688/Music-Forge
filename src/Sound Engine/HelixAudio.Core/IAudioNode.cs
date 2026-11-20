using System;

namespace HelixAudio.Core
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
        /// Processes an audio buffer and returns the processed output.
        /// Override this method in derived classes to implement custom processing logic.
        /// </summary>
        /// <param name="buffer">The input audio buffer to process.</param>
        /// <returns>The processed audio buffer.</returns>
        float[] Process(float[] buffer);

        /// <summary>
        /// Resets the node state to initial values.
        /// Called before processing begins to ensure clean state.
        /// </summary>
        void Reset();
    }
}