using System;
using System.IO;
using System.Text;

namespace HelixAudio.IO
{
    /// <summary>
    /// Writes WAV (RIFF/WAVE) audio files.
    /// </summary>
    public class WavWriter : IDisposable
    {
        private Stream _stream;
        private BinaryWriter _writer;
        private bool _disposed;
        private bool _finalized;
        private long _dataSizePosition;
        private long _dataStartPosition;
        private long _frameCount;
        private int _bitsPerSample;

        /// <summary>
        /// Gets the sample rate in Hz.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Gets the number of audio channels.
        /// </summary>
        public int Channels { get; private set; }

        /// <summary>
        /// Gets the bits per sample.
        /// </summary>
        public int BitsPerSample => _bitsPerSample;

        /// <summary>
        /// Gets the total number of frames written.
        /// </summary>
        public long FrameCount => _frameCount;

        /// <summary>
        /// Initializes a new instance of the WavWriter class.
        /// </summary>
        /// <param name="path">Path to write the WAV file.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="bitsPerSample">Bits per sample (8, 16, 24, or 32).</param>
        public WavWriter(string path, int sampleRate, int channels, int bitsPerSample = 16)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            Initialize(sampleRate, channels, bitsPerSample);
        }

        /// <summary>
        /// Initializes a new instance of the WavWriter class from a stream.
        /// </summary>
        /// <param name="stream">Stream to write WAV data to.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="bitsPerSample">Bits per sample (8, 16, 24, or 32).</param>
        public WavWriter(Stream stream, int sampleRate, int channels, int bitsPerSample = 16)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable.", nameof(stream));

            _stream = stream;
            Initialize(sampleRate, channels, bitsPerSample);
        }

        /// <summary>
        /// Writes a block of frames to the WAV file.
        /// </summary>
        /// <param name="buffer">Interleaved samples (float values -1.0 to 1.0).</param>
        public void WriteFrames(float[] buffer)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WavWriter));

            if (_finalized)
                throw new InvalidOperationException("Cannot write after Complete() has been called.");

            if (buffer == null || buffer.Length == 0)
                return;

            int samplesPerFrame = Channels;
            int frameCount = buffer.Length / samplesPerFrame;

            if (buffer.Length % samplesPerFrame != 0)
                throw new ArgumentException("Buffer length must be a multiple of channel count.");

            // Write samples based on bit depth
            if (_bitsPerSample == 8)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    byte sample = (byte)((buffer[i] + 1.0f) * 127.5f);
                    _writer.Write(sample);
                }
            }
            else if (_bitsPerSample == 16)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    short sample = (short)(buffer[i] * 32767.0f);
                    _writer.Write(sample);
                }
            }
            else if (_bitsPerSample == 24)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    int sample = (int)(buffer[i] * 8388607.0f);
                    Write24BitInt(sample);
                }
            }
            else if (_bitsPerSample == 32)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    int sample = (int)(buffer[i] * 2147483647.0f);
                    _writer.Write(sample);
                }
            }
            else
            {
                throw new NotSupportedException($"Bits per sample {_bitsPerSample} not supported.");
            }

            _frameCount += frameCount;
        }

        /// <summary>
        /// Finalizes the WAV file, updating headers with correct sizes.
        /// </summary>
        public void Complete()
        {
            if (_finalized)
                return;

            if (_disposed)
                throw new ObjectDisposedException(nameof(WavWriter));

            _finalized = true;

            // Calculate total file size
            long dataSize = _frameCount * Channels * (_bitsPerSample / 8);
            long fileSize = dataSize + 44 - 8; // WAV header is 44 bytes

            // Update RIFF chunk size
            _stream.Seek(4, SeekOrigin.Begin);
            _writer.Write((uint)fileSize);

            // Update data chunk size
            _stream.Seek(_dataSizePosition, SeekOrigin.Begin);
            _writer.Write((uint)dataSize);

            // Seek to end of file
            _stream.Seek(0, SeekOrigin.End);
        }

        /// <summary>
        /// Disposes the WavWriter and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (!_finalized)
                    Complete();

                _writer?.Dispose();
                _stream?.Dispose();
                _disposed = true;
            }
        }

        private void Initialize(int sampleRate, int channels, int bitsPerSample)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            if (channels <= 0)
                throw new ArgumentException("Channel count must be positive.", nameof(channels));

            if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
                throw new ArgumentException("Bits per sample must be 8, 16, 24, or 32.", nameof(bitsPerSample));

            SampleRate = sampleRate;
            Channels = channels;
            _bitsPerSample = bitsPerSample;
            _frameCount = 0;
            _disposed = false;
            _finalized = false;

            _writer = new BinaryWriter(_stream);

            // Write WAV header
            WriteHeader();
        }

        private void WriteHeader()
        {
            int bytesPerSample = _bitsPerSample / 8;
            short audioFormat = 1; // PCM

            // RIFF header
            _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write((uint)0); // Placeholder - will be updated in Complete()
            _writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // Format chunk
            _writer.Write(Encoding.ASCII.GetBytes("fmt "));
            _writer.Write((uint)16); // Chunk size
            _writer.Write(audioFormat);
            _writer.Write((short)Channels);
            _writer.Write(SampleRate);
            _writer.Write(SampleRate * Channels * bytesPerSample); // Byte rate
            _writer.Write((short)(Channels * bytesPerSample)); // Block align
            _writer.Write((short)_bitsPerSample);

            // Data chunk
            _writer.Write(Encoding.ASCII.GetBytes("data"));
            _dataSizePosition = _stream.Position;
            _writer.Write((uint)0); // Placeholder - will be updated in Complete()
            _dataStartPosition = _stream.Position;
        }

        private void Write24BitInt(int value)
        {
            byte b1 = (byte)((value >> 0) & 0xFF);
            byte b2 = (byte)((value >> 8) & 0xFF);
            byte b3 = (byte)((value >> 16) & 0xFF);
            
            _writer.Write(b1);
            _writer.Write(b2);
            _writer.Write(b3);
        }
    }
}








