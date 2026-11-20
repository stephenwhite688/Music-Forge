using System;
using HelixAudio.IO;

namespace HelixAudio.Streaming
{
    public class StreamReader : IDisposable
    {
        private readonly object _reader;
        private readonly ReaderType _readerType;
        private bool _disposed;

        private enum ReaderType
        {
            WavReader,
            PcmReader
        }

        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public int BitsPerSample { get; private set; }

        public long Position
        {
            get
            {
                return _readerType switch
                {
                    ReaderType.WavReader => ((WavReader)_reader).Position,
                    ReaderType.PcmReader => ((PcmReader)_reader).Position,
                    _ => throw new InvalidOperationException("Invalid reader type.")
                };
            }
        }

        public long TotalFrames
        {
            get
            {
                return _readerType switch
                {
                    ReaderType.WavReader => ((WavReader)_reader).TotalFrames,
                    ReaderType.PcmReader => ((PcmReader)_reader).TotalFrames,
                    _ => throw new InvalidOperationException("Invalid reader type.")
                };
            }
        }

        public bool HasMoreFrames => Position < TotalFrames;

        public StreamReader(WavReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            _reader = reader;
            _readerType = ReaderType.WavReader;
            _disposed = false;

            SampleRate = reader.SampleRate;
            Channels = reader.Channels;
            BitsPerSample = reader.BitsPerSample;
        }

        public StreamReader(PcmReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            _reader = reader;
            _readerType = ReaderType.PcmReader;
            _disposed = false;

            SampleRate = reader.SampleRate;
            Channels = reader.Channels;
            BitsPerSample = reader.BitsPerSample;
        }

        public float[] ReadChunk(int frames)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StreamReader));

            if (frames <= 0)
                return new float[0];

            return _readerType switch
            {
                ReaderType.WavReader => ((WavReader)_reader).ReadFrames(frames),
                ReaderType.PcmReader => ((PcmReader)_reader).ReadFrames(frames),
                _ => throw new InvalidOperationException("Invalid reader type.")
            };
        }

        public void Seek(long frame)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StreamReader));

            switch (_readerType)
            {
                case ReaderType.WavReader:
                    ((WavReader)_reader).Seek(frame);
                    break;
                case ReaderType.PcmReader:
                    ((PcmReader)_reader).Seek(frame);
                    break;
                default:
                    throw new InvalidOperationException("Invalid reader type.");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                switch (_readerType)
                {
                    case ReaderType.WavReader:
                        ((WavReader)_reader)?.Dispose();
                        break;
                    case ReaderType.PcmReader:
                        ((PcmReader)_reader)?.Dispose();
                        break;
                }
                _disposed = true;
            }
        }
    }
}
