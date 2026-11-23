using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.Core
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
        /// Algorithmic latency of this node in frames at the current format.
        /// Nodes that delay, look ahead, or filter with group delay should
        /// override. Used for latency reporting and (future) PDC.
        /// </summary>
        public virtual int LatencyFrames => 0;

        /// <summary>
        /// Describes the automatable parameters this node exposes.
        /// Override to advertise parameters; default is none.
        /// </summary>
        public virtual IReadOnlyList<AudioParameterInfo> GetParameterInfo()
            => Array.Empty<AudioParameterInfo>();

        /// <summary>
        /// Reads a parameter value by name. Returns false if unknown.
        /// Safe to call from the control thread while processing.
        /// </summary>
        public virtual bool TryGetParameter(string name, out float value)
        {
            value = 0f;
            return false;
        }

        /// <summary>
        /// Writes a parameter value by name (clamped to the descriptor's range).
        /// Returns false if the parameter is unknown or read-only.
        /// Implementations must be safe to call while Process() is running.
        /// </summary>
        public virtual bool TrySetParameter(string name, float value)
        {
            return false;
        }

        /// <summary>
        /// Serializes the node's full state (parameters + configuration) to JSON.
        /// Used for presets, sessions and plugin state chunks. Default: empty object.
        /// </summary>
        public virtual JsonObject GetState() => new JsonObject();

        /// <summary>
        /// Restores state previously produced by <see cref="GetState"/>.
        /// Unknown/missing fields are ignored; invalid values keep defaults.
        /// </summary>
        public virtual void SetState(JsonObject state) { }

        /// <summary>
        /// Processes an interleaved audio buffer in place.
        /// Override this method in derived classes to implement custom processing logic.
        /// </summary>
        /// <param name="buffer">The interleaved audio buffer to process in place.</param>
        public abstract void Process(Span<float> buffer);
    }
}