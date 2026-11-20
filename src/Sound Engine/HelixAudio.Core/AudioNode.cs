using System;

namespace HelixAudio.Core
{
    /// <summary>
    /// Abstract base class for audio processing nodes that implement the IAudioNode interface.
    /// Provides default implementations for Prepare() and Reset() methods, and requires derived classes to implement Process().
    /// </summary>
    public abstract class AudioNode : IAudioNode
    {
        /// <summary>
        /// Gets the name of this audio node.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the sample rate in Hz.
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Gets the number of audio channels.
        /// </summary>
        public int Channels { get; }

        /// <summary>
        /// Gets or sets the audio format associated with this node.
        /// Set by the Prepare() method when called by the engine.
        /// </summary>
        public AudioFormat Format { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AudioNode class.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="name">Optional node name. If null or empty, uses the type name as default.</param>
        protected AudioNode(int sampleRate, int channels, string name)
        {
            SampleRate = sampleRate;
            Channels = channels;
            Name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
        }

        /// <summary>
        /// Prepares the node for processing with the specified format.
        /// Override this method to perform format-specific initialization.
        /// </summary>
        /// <param name="format">The audio format to prepare for.</param>
        public virtual void Prepare(AudioFormat format)
        {
            Format = format;
        }

        /// <summary>
        /// Resets the node state to initial values.
        /// Override this method to reset any per-node state variables before processing begins.
        /// </summary>
        public virtual void Reset() { }

        /// <summary>
        /// Processes an audio buffer and returns the processed output.
        /// Override this method in derived classes to implement custom processing logic.
        /// </summary>
        /// <param name="buffer">The input audio buffer to process.</param>
        /// <returns>The processed audio buffer.</returns>
        public abstract float[] Process(float[] buffer);
    }
}