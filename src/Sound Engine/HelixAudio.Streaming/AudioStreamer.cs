using System;

namespace HelixAudio.Streaming
{
    public class AudioStreamer : IDisposable
    {
        public enum StreamState
        {
            Stopped,
            Running,
            Paused
        }

        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly int _chunkSizeFrames;

        // FIX: Make this properly nullable
        private AudioPipeline? _pipeline;

        private StreamState _state;
        private bool _disposed;
        private long _totalFramesProcessed;

        public StreamState State => _state;
        public int SampleRate => _reader.SampleRate;
        public int Channels => _reader.Channels;
        public int BitsPerSample => _reader.BitsPerSample;
        public long TotalFramesProcessed => _totalFramesProcessed;
        public long TotalFrames => _reader.TotalFrames;
        public long Position => _reader.Position;

        public float Progress
        {
            get
            {
                if (TotalFrames == 0)
                    return 0.0f;
                return (float)Position / TotalFrames;
            }
        }

        public AudioStreamer(StreamReader reader, StreamWriter writer, int chunkSizeFrames = 1024)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (chunkSizeFrames <= 0)
                throw new ArgumentException("Chunk size must be positive.", nameof(chunkSizeFrames));

            _reader = reader;
            _writer = writer;
            _chunkSizeFrames = chunkSizeFrames;
            _state = StreamState.Stopped;
            _disposed = false;
            _totalFramesProcessed = 0;

            // FIX: This is allowed because _pipeline is nullable
            _pipeline = null;
        }

        public void Start(Func<float[], float[]> processor = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioStreamer));
            if (_state == StreamState.Running)
                return;

            if (_state == StreamState.Stopped)
            {
                _reader.Seek(0);
                _totalFramesProcessed = 0;
            }

            if (_state == StreamState.Paused)
            {
                _state = StreamState.Running;
                return;
            }

            var processFunc = processor ?? (input => input);

            // FIX: _pipeline is nullable, so this is valid
            _pipeline = new AudioPipeline(_reader, processFunc, _writer);

            _state = StreamState.Running;
            ProcessLoop();
        }

        public void Pause()
        {
            if (_state == StreamState.Running)
                _state = StreamState.Paused;
        }

        public void Resume()
        {
            if (_state == StreamState.Paused)
            {
                _state = StreamState.Running;
                ProcessLoop();
            }
        }

        public void Stop()
        {
            if (_state == StreamState.Stopped)
                return;

            _state = StreamState.Stopped;

            if (_pipeline != null)
            {
                _pipeline.Dispose();
                _pipeline = null;
            }

            _writer.Flush();
        }

        private void ProcessLoop()
        {
            // FIX: Ensure _pipeline is not null
            if (_pipeline == null)
                return;

            while (_state == StreamState.Running)
            {
                long processed = _pipeline.Process(_chunkSizeFrames);

                if (processed == 0)
                {
                    _state = StreamState.Stopped;
                    _writer.Flush();
                    break;
                }

                _totalFramesProcessed += processed;
            }
        }

        public void StartWithProgress(Func<float[], float[]> processor, Action<long, long> progressCallback)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioStreamer));
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));
            if (progressCallback == null)
                throw new ArgumentNullException(nameof(progressCallback));
            if (_state == StreamState.Running)
                return;

            _reader.Seek(0);
            _totalFramesProcessed = 0;

            var pipeline = new AudioPipeline(_reader, processor, _writer);
            _state = StreamState.Running;

            long totalFrames = _reader.TotalFrames;
            long processed = 0;

            while (_state == StreamState.Running && _reader.HasMoreFrames)
            {
                float[] input = _reader.ReadChunk(_chunkSizeFrames);
                if (input == null || input.Length == 0)
                    break;

                if (_state == StreamState.Paused)
                {
                    while (_state == StreamState.Paused)
                        System.Threading.Thread.Sleep(10);
                    continue;
                }

                float[] output = processor(input);

                if (output != null && output.Length > 0)
                {
                    _writer.WriteChunk(output);

                    int framesInChunk = output.Length / _reader.Channels;
                    processed += framesInChunk;
                    _totalFramesProcessed += framesInChunk;

                    progressCallback(processed, totalFrames);
                }
            }

            _writer.Flush();
            _state = StreamState.Stopped;
            pipeline.Dispose();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _reader?.Dispose();
                _writer?.Dispose();
                _disposed = true;
            }
        }
    }
}
