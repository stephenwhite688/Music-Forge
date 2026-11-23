using System;
using System.IO;

namespace AudioForge.IO
{
    /// <summary>
    /// Writes raw PCM audio files with support for 8-bit, 16-bit, and 32-bit sample depths.
    /// </summary>
    public class PcmWriter : IDisposable
    {
        private Stream _stream;
        private BinaryWriter _writer;
        private bool _disposed;
        private long _frameCount;
        private int _sampleRate;
        private int _channels;
        private int _bitsPerSample;

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
        /// Gets the number of frames written so far.
        /// </summary>
        public long FrameCount => _frameCount;

        /// <summary>
        /// Initializes a new instance of the PcmWriter class with a file path.
        /// </summary>
        /// <param name="path">Path to the PCM file to create.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        /// <param name="bitsPerSample">Bits per sample (8, 16, or 32). Default is 16.</param>
        public PcmWriter(string path, int sampleRate, int channels, int bitsPerSample = 16)
        {
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            Initialize(sampleRate, channels, bitsPerSample);
        }

        /// <summary>
        /// Initializes a new instance of the PcmWriter class with an existing stream.
        /// </summary>
        /// <param name="stream">The underlying stream to write to.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        /// <param name="bitsPerSample">Bits per sample (8, 16, or 32). Default is 16.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the stream is not writable.</exception>
        public PcmWriter(Stream stream, int sampleRate, int channels, int bitsPerSample = 16)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!_stream.CanWrite)
                throw new ArgumentException("Stream must be writable.", nameof(stream));
            Initialize(sampleRate, channels, bitsPerSample);
        }

        /// <summary>
        /// Writes audio frames (float samples normalized to -1.0 to 1.0) to the PCM file.
        /// </summary>
        /// <param name="buffer">Array of interleaved float samples to write.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when the buffer length is not a multiple of channel count.</exception>
        public void WriteFrames(float[] buffer)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PcmWriter));
            if (buffer == null || buffer.Length == 0) return;

            int samplesPerFrame = _channels;
            int frameCount = buffer.Length / samplesPerFrame;
            if (buffer.Length % samplesPerFrame != 0)
                throw new ArgumentException("Buffer length must be a multiple of channel count.", nameof(buffer));

            if (_bitsPerSample == 8)
            {
                for (int i = 0; i < buffer.Length; i++)
                    _writer.Write((byte)((buffer[i] + 1.0f) * 127.5f));
            }
            else if (_bitsPerSample == 16)
            {
                for (int i = 0; i < buffer.Length; i++)
                    _writer.Write((short)(buffer[i] * 32767.0f));
            }
            else if (_bitsPerSample == 32)
            {
                for (int i = 0; i < buffer.Length; i++)
                    _writer.Write((int)(buffer[i] * 2147483647.0f));
            }
            else
            {
                throw new NotSupportedException($"Bits per sample {_bitsPerSample} not supported.");
            }

            _frameCount += frameCount;
        }

        /// <summary>
        /// Releases all resources used by the PcmWriter.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _writer?.Dispose();
                _stream?.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Initializes the PCM writer with the specified sample rate, channels, and bit depth.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        /// <param name="bitsPerSample">Bits per sample (8, 16, or 32).</param>
        private void Initialize(int sampleRate, int channels, int bitsPerSample)
        {
            if (sampleRate <= 0) throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            if (channels <= 0) throw new ArgumentException("Channel count must be positive.", nameof(channels));
            if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 32)
                throw new ArgumentException("Bits per sample must be 8, 16, or 32.", nameof(bitsPerSample));

            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = bitsPerSample;
            _frameCount = 0;
            _disposed = false;
            _writer = new BinaryWriter(_stream);
        }
    }
}