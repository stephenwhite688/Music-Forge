using System;
using HelixAudio.IO;

namespace HelixAudio.Streaming
{
    public class StreamWriter : IDisposable
    {
        private readonly object _writer;
        private readonly WriterType _writerType;
        private bool _disposed;

        private enum WriterType
        {
            WavWriter,
            PcmWriter
        }

        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public int BitsPerSample { get; private set; }

        public long FrameCount
        {
            get
            {
                return _writerType switch
                {
                    WriterType.WavWriter => ((WavWriter)_writer).FrameCount,
                    WriterType.PcmWriter => ((PcmWriter)_writer).FrameCount,
                    _ => throw new InvalidOperationException("Invalid writer type.")
                };
            }
        }

        public StreamWriter(WavWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            _writer = writer;
            _writerType = WriterType.WavWriter;
            _disposed = false;

            SampleRate = writer.SampleRate;
            Channels = writer.Channels;
            BitsPerSample = writer.BitsPerSample;
        }

        public StreamWriter(PcmWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            _writer = writer;
            _writerType = WriterType.PcmWriter;
            _disposed = false;

            SampleRate = writer.SampleRate;
            Channels = writer.Channels;
            BitsPerSample = writer.BitsPerSample;
        }

        public void WriteChunk(float[] frames)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StreamWriter));

            if (frames == null || frames.Length == 0)
                return;

            switch (_writerType)
            {
                case WriterType.WavWriter:
                    ((WavWriter)_writer).WriteFrames(frames);
                    break;
                case WriterType.PcmWriter:
                    ((PcmWriter)_writer).WriteFrames(frames);
                    break;
                default:
                    throw new InvalidOperationException("Invalid writer type.");
            }
        }

        public void Flush()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(StreamWriter));

            switch (_writerType)
            {
                case WriterType.WavWriter:
                    ((WavWriter)_writer).Complete();
                    break;
                case WriterType.PcmWriter:
                    break;
                default:
                    throw new InvalidOperationException("Invalid writer type.");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Flush();

                switch (_writerType)
                {
                    case WriterType.WavWriter:
                        ((WavWriter)_writer)?.Dispose();
                        break;
                    case WriterType.PcmWriter:
                        ((PcmWriter)_writer)?.Dispose();
                        break;
                }
                _disposed = true;
            }
        }
    }
}
