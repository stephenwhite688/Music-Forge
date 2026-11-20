using System;

namespace HelixAudio.Streaming
{
    /// <summary>
    /// Processing pipeline connecting reader → processor → writer.
    /// </summary>
    public class AudioPipeline : IDisposable
    {
        private readonly StreamReader _reader;
        private readonly Func<float[], float[]> _processor;
        private readonly StreamWriter _writer;
        private bool _disposed;
        private long _totalFramesProcessed;

        public long TotalFramesProcessed => _totalFramesProcessed;
        public StreamReader Reader => _reader;
        public StreamWriter Writer => _writer;

        public AudioPipeline(StreamReader reader, Func<float[], float[]> processor, StreamWriter writer)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (reader.SampleRate != writer.SampleRate)
                throw new ArgumentException("Reader and writer sample rates must match.");
            if (reader.Channels != writer.Channels)
                throw new ArgumentException("Reader and writer channel counts must match.");
            if (reader.BitsPerSample != writer.BitsPerSample)
                throw new ArgumentException("Reader and writer bit depths must match.");

            _reader = reader;
            _processor = processor;
            _writer = writer;
            _disposed = false;
            _totalFramesProcessed = 0;
        }

        public long Process(int chunkSizeFrames)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioPipeline));
            if (chunkSizeFrames <= 0)
                throw new ArgumentException("Chunk size must be positive.", nameof(chunkSizeFrames));

            long framesProcessed = 0;

            while (_reader.HasMoreFrames)
            {
                float[] input = _reader.ReadChunk(chunkSizeFrames);
                if (input == null || input.Length == 0)
                    break;

                float[] output = _processor(input);
                if (output == null || output.Length == 0)
                    continue;

                _writer.WriteChunk(output);

                int framesInChunk = output.Length / _reader.Channels;
                framesProcessed += framesInChunk;
                _totalFramesProcessed += framesInChunk;
            }

            _writer.Flush();
            return framesProcessed;
        }

        public long ProcessWithProgress(int chunkSizeFrames, Action<long, long> progressCallback)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioPipeline));
            if (chunkSizeFrames <= 0)
                throw new ArgumentException("Chunk size must be positive.", nameof(chunkSizeFrames));
            if (progressCallback == null)
                throw new ArgumentNullException(nameof(progressCallback));

            long totalFrames = _reader.TotalFrames;
            long framesProcessed = 0;

            while (_reader.HasMoreFrames)
            {
                float[] input = _reader.ReadChunk(chunkSizeFrames);
                if (input == null || input.Length == 0)
                    break;

                float[] output = _processor(input);

                if (output != null && output.Length > 0)
                {
                    _writer.WriteChunk(output);

                    int framesInChunk = output.Length / _reader.Channels;
                    framesProcessed += framesInChunk;
                    _totalFramesProcessed += framesInChunk;

                    progressCallback(framesProcessed, totalFrames);
                }
            }

            _writer.Flush();
            return framesProcessed;
        }

        public void Reset()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioPipeline));

            _reader.Seek(0);
            _totalFramesProcessed = 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _disposed = true;
            }
        }
    }
}
