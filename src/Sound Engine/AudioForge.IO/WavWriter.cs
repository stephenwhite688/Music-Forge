using System;
using System.IO;
using System.Text;

namespace AudioForge.IO
{
    /// <summary>Dither mode applied when writing integer PCM from float.</summary>
    public enum DitherMode
    {
        /// <summary>No dither - plain rounding (audible quantization noise).</summary>
        None,
        /// <summary>Triangular probability density, ±1 LSB - standard for 16/24-bit delivery.</summary>
        Tpdf
    }

    /// <summary>
    /// Writes WAV (RIFF/WAVE) audio files - integer PCM or IEEE float32.
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
        private readonly bool _isFloat;
        private readonly Random _ditherRng = new();

        /// <summary>True when writing IEEE float32 samples (format tag 3).</summary>
        public bool IsFloat => _isFloat;

        /// <summary>Dither applied on integer output. Default None.</summary>
        public DitherMode Dither { get; set; } = DitherMode.None;

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
        /// <param name="isFloat">Write IEEE float32 (format tag 3). Forces 32 bits.</param>
        public WavWriter(string path, int sampleRate, int channels, int bitsPerSample = 16, bool isFloat = false)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            _isFloat = isFloat;
            Initialize(sampleRate, channels, isFloat ? 32 : bitsPerSample);
        }

        /// <summary>
        /// Initializes a new instance of the WavWriter class from a stream.
        /// </summary>
        /// <param name="stream">Stream to write WAV data to.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="bitsPerSample">Bits per sample (8, 16, 24, or 32).</param>
        /// <param name="isFloat">Write IEEE float32 (format tag 3). Forces 32 bits.</param>
        public WavWriter(Stream stream, int sampleRate, int channels, int bitsPerSample = 16, bool isFloat = false)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable.", nameof(stream));

            _stream = stream;
            _isFloat = isFloat;
            Initialize(sampleRate, channels, isFloat ? 32 : bitsPerSample);
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

            if (_isFloat)
            {
                for (int i = 0; i < buffer.Length; i++)
                    _writer.Write(buffer[i]);
                _frameCount += frameCount;
                return;
            }

            // Write samples based on bit depth
            if (_bitsPerSample == 8)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    long v = (long)QuantizeToInt(buffer[i], 128.0) + 128;
                    _writer.Write((byte)Math.Clamp(v, 0, 255));
                }
            }
            else if (_bitsPerSample == 16)
            {
                for (int i = 0; i < buffer.Length; i++)
                    _writer.Write((short)Math.Clamp((long)QuantizeToInt(buffer[i], 32768.0), -32768, 32767));
            }
            else if (_bitsPerSample == 24)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    long v = (long)QuantizeToInt(buffer[i], 8388608.0);
                    Write24BitInt((int)Math.Clamp(v, -8388608, 8388607));
                }
            }
            else if (_bitsPerSample == 32)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    long v = (long)QuantizeToInt(buffer[i], 2147483648.0);
                    _writer.Write((int)Math.Clamp(v, int.MinValue, int.MaxValue));
                }
            }
            else
            {
                throw new NotSupportedException($"Bits per sample {_bitsPerSample} not supported.");
            }

            _frameCount += frameCount;
        }

        /// <summary>
        /// Quantizes a float sample to an integer grid of the given scale
        /// (e.g. 32768 for 16-bit), applying ±1 LSB TPDF dither when enabled.
        /// </summary>
        private double QuantizeToInt(float sample, double scale)
        {
            double v = Math.Clamp(sample, -1.0, 1.0) * scale;
            if (Dither == DitherMode.Tpdf)
                v += _ditherRng.NextDouble() + _ditherRng.NextDouble() - 1.0;
            return Math.Round(v, MidpointRounding.AwayFromZero);
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
            short audioFormat = (short)(_isFloat ? 3 : 1); // 3 = IEEE float, 1 = PCM

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








