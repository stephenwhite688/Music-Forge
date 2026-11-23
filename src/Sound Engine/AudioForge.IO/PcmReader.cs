using System;
using System.IO;

namespace AudioForge.IO
{
    /// <summary>
    /// Reads raw PCM audio files.
    /// </summary>
    public class PcmReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private bool _disposed;
        private long _position;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bitsPerSample;
        private readonly int _bytesPerSample;
        private readonly long _totalFrames;

        /// <summary>
        /// Gets the sample rate in Hz.
        /// </summary>
        public int SampleRate => _sampleRate;

        /// <summary>
        /// Gets the number of audio channels.
        /// </summary>
        public int Channels => _channels;

        /// <summary>
        /// Gets the bits per sample.
        /// </summary>
        public int BitsPerSample => _bitsPerSample;

        /// <summary>
        /// Gets the total number of frames in the file.
        /// </summary>
        public long TotalFrames => _totalFrames;

        /// <summary>
        /// Gets the position in frames.
        /// </summary>
        public long Position => _position;

        /// <summary>
        /// Initializes a new instance of the PcmReader class.
        /// </summary>
        /// <param name="path">Path to the PCM file.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="bitsPerSample">Bits per sample (8, 16, or 32).</param>
        public PcmReader(string path, int sampleRate, int channels, int bitsPerSample = 16)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_stream);
            _disposed = false;
            _position = 0;

            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = bitsPerSample;
            _bytesPerSample = bitsPerSample / 8;

            if (_bitsPerSample != 8 && _bitsPerSample != 16 && _bitsPerSample != 32)
                throw new ArgumentException("Bits per sample must be 8, 16, or 32.");

            _totalFrames = _stream.Length / (_channels * _bytesPerSample);
        }

        /// <summary>
        /// Reads a specified number of frames from the PCM file.
        /// </summary>
        /// <param name="count">Number of frames to read.</param>
        /// <returns>Array of interleaved samples (float values -1.0 to 1.0).</returns>
        public float[] ReadFrames(int count)
        {
            if (count <= 0)
                return new float[0];

            if (_disposed)
                throw new ObjectDisposedException(nameof(PcmReader));

            if (_position >= _totalFrames)
                return new float[0];

            int framesToRead = (int)Math.Min(count, _totalFrames - _position);
            int samplesToRead = framesToRead * _channels;
            float[] samples = new float[samplesToRead];

            // Read based on bit depth
            if (_bitsPerSample == 8)
            {
                for (int i = 0; i < samplesToRead; i++)
                {
                    byte sample = _reader.ReadByte();
                    samples[i] = (sample / 127.5f) - 1.0f;
                }
            }
            else if (_bitsPerSample == 16)
            {
                for (int i = 0; i < samplesToRead; i++)
                {
                    short sample = _reader.ReadInt16();
                    samples[i] = sample / 32768.0f;
                }
            }
            else if (_bitsPerSample == 32)
            {
                for (int i = 0; i < samplesToRead; i++)
                {
                    int sample = _reader.ReadInt32();
                    samples[i] = sample / 2147483648.0f;
                }
            }

            _position += framesToRead;
            return samples;
        }

        /// <summary>
        /// Seeks to a specific frame position.
        /// </summary>
        /// <param name="frame">Frame position.</param>
        public void Seek(long frame)
        {
            if (frame < 0 || frame > _totalFrames)
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame position out of range.");

            _position = frame;
            _stream.Seek(frame * _channels * _bytesPerSample, SeekOrigin.Begin);
        }

        /// <summary>
        /// Disposes the PcmReader and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _stream?.Dispose();
                _disposed = true;
            }
        }
    }
}
