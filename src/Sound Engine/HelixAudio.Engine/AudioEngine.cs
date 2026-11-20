using System;
using System.Collections.Generic;
using HelixAudio.Core;

namespace HelixAudio.Engine
{
    /// <summary>
    /// Main audio engine that manages the audio processing graph and device interaction.
    /// </summary>
    public class AudioEngine : EngineBase, IDisposable
    {
        /// <summary>
        /// Represents the current state of the audio engine.
        /// </summary>
        public enum EngineState
        {
            /// <summary>The engine is not running.</summary>
            Stopped,
            
            /// <summary>The engine is actively processing audio.</summary>
            Running,
            
            /// <summary>The engine is paused and not processing audio.</summary>
            Paused
        }

        private readonly ProcessingGraph _graph;
        private readonly IAudioDeviceManager _deviceManager;
        private readonly object _lock = new object();
        private EngineState _state;
        private bool _disposed;

        /// <summary>
        /// Gets the processing graph containing all audio nodes.
        /// </summary>
        public ProcessingGraph Graph => _graph;

        /// <summary>
        /// Gets or sets the current engine state.
        /// </summary>
        public EngineState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
            private set
            {
                lock (_lock)
                {
                    if (_state != value)
                    {
                        _state = value;
                        RaiseStateChanged(value);
                    }
                }
            }
        }

        /// <summary>
        /// Occurs when the engine state changes.
        /// </summary>
        public event EventHandler<EngineState> StateChanged = delegate { };

        /// <summary>
        /// Occurs when an error occurs in the engine.
        /// </summary>
        public event EventHandler<string> ErrorOccurred = delegate { };

        /// <summary>
        /// Initializes a new instance of the AudioEngine class.
        /// </summary>
        /// <param name="sampleRate">The audio sample rate in Hz.</param>
        /// <param name="channels">The number of audio channels.</param>
        /// <param name="deviceManager">The device manager for audio device operations.</param>
        public AudioEngine(int sampleRate, int channels, IAudioDeviceManager deviceManager)
            : base(sampleRate, channels)
        {
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _graph = new ProcessingGraph();
            _state = EngineState.Stopped;
            _disposed = false;

            try
            {
                _deviceManager.Initialize(sampleRate, channels);
            }
            catch (Exception ex)
            {
                RaiseError($"Failed to initialize device manager: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the processing graph.
        /// </summary>
        public ProcessingGraph ProcessingGraph => _graph;

        /// <summary>
        /// Adds a node to the audio processing graph.
        /// </summary>
        /// <param name="node">The audio node to add.</param>
        /// <returns>The added node.</returns>
        public EngineNode AddNode(EngineNode node)
        {
            lock (_lock)
            {
                return _graph.AddNode(node);
            }
        }

        /// <summary>
        /// Removes a node from the audio processing graph.
        /// </summary>
        /// <param name="id">The ID of the node to remove.</param>
        /// <returns><c>true</c> if the node was removed, <c>false</c> otherwise.</returns>
        public bool RemoveNode(Guid id)
        {
            lock (_lock)
            {
                return _graph.RemoveNode(id);
            }
        }

        /// <summary>
        /// Connects two nodes in the audio graph.
        /// </summary>
        /// <param name="fromId">The ID of the source node.</param>
        /// <param name="toId">The ID of the destination node.</param>
        public void Connect(Guid fromId, Guid toId)
        {
            lock (_lock)
            {
                _graph.Connect(fromId, toId);
            }
        }

        /// <summary>
        /// Disconnects two nodes in the audio graph.
        /// </summary>
        /// <param name="fromId">The ID of the source node.</param>
        /// <param name="toId">The ID of the destination node.</param>
        public void Disconnect(Guid fromId, Guid toId)
        {
            lock (_lock)
            {
                _graph.Disconnect(fromId, toId);
            }
        }

        /// <summary>
        /// Starts the audio engine and begins processing.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(AudioEngine));

                if (_state == EngineState.Running)
                    return;

                try
                {
                    _deviceManager.Start();
                    State = EngineState.Running;
                    AudioDiagnostics.Log("Engine", $"Started; sampleRate={SampleRate}; channels={Channels}");
                }
                catch (Exception ex)
                {
                    RaiseError($"Failed to start engine: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops the audio engine and halts processing.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                if (_state == EngineState.Stopped)
                    return;

                try
                {
                    _deviceManager.Stop();
                    State = EngineState.Stopped;
                    AudioDiagnostics.Log("Engine", "Stopped");
                }
                catch (Exception ex)
                {
                    RaiseError($"Failed to stop engine: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Pauses the audio engine and halts processing temporarily.
        /// </summary>
        public void Pause()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                if (_state != EngineState.Running)
                    return;

                try
                {
                    _deviceManager.Pause();
                    State = EngineState.Paused;
                    AudioDiagnostics.Log("Engine", "Paused");
                }
                catch (Exception ex)
                {
                    RaiseError($"Failed to pause engine: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Resumes the audio engine after it was paused.
        /// </summary>
        public void Resume()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                if (_state != EngineState.Paused)
                    return;

                try
                {
                    _deviceManager.Resume();
                    State = EngineState.Running;
                    AudioDiagnostics.Log("Engine", "Resumed");
                }
                catch (Exception ex)
                {
                    RaiseError($"Failed to resume engine: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Processes audio through the graph and returns the output buffer.
        /// </summary>
        /// <param name="input">The input audio buffer.</param>
        /// <returns>The processed audio buffer.</returns>
        public float[] Process(float[] input)
        {
            lock (_lock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(AudioEngine));

                if (_state != EngineState.Running)
                    return input;

                try
                {
                    return _graph.Process(input);
                }
                catch (Exception ex)
                {
                    RaiseError($"Error processing audio: {ex.Message}");
                    return input;
                }
            }
        }

        /// <summary>
        /// Releases resources used by the AudioEngine.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lock)
                {
                    if (_state != EngineState.Stopped)
                    {
                        try
                        {
                            _deviceManager.Stop();
                        }
                        catch { }
                    }

                    _graph.Clear();
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// Raises the StateChanged event.
        /// </summary>
        /// <param name="state">The new engine state.</param>
        protected virtual void RaiseStateChanged(EngineState state)
        {
            StateChanged?.Invoke(this, state);
        }

        /// <summary>
        /// Raises the ErrorOccurred event.
        /// </summary>
        /// <param name="message">The error message.</param>
        protected virtual void RaiseError(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }
    }
}