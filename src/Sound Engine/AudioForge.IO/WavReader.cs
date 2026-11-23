using System;
using System.IO;
using System.Text;

namespace AudioForge.IO
{
    /// <summary>
    /// Reads WAV (RIFF/WAVE) audio files.
    /// </summary>
    public class WavReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private bool _disposed;
        private long _dataStartPosition;
        private long _dataLength;
        private bool _isFloat;

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
        public int BitsPerSample { get; private set; }

        /// <summary>
        /// Gets the total number of frames in the file.
        /// </summary>
        public long TotalFrames { get; private set; }

        /// <summary>
        /// Gets the position in frames.
        /// </summary>
        public long Position { get; private set; }

        /// <summary>
        /// Initializes a new instance of the WavReader class.
        /// </summary>
        /// <param name="path">Path to the WAV file.</param>
        public WavReader(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_stream);
            _disposed = false;
            Position = 0;

            ReadHeader();
        }

        /// <summary>
        /// Initializes a new instance of the WavReader class from a stream.
        /// </summary>
        /// <param name="stream">Stream containing WAV data.</param>
        public WavReader(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(stream));

            _stream = stream;
            _reader = new BinaryReader(_stream);
            _disposed = false;
            Position = 0;

            ReadHeader();
        }

        /// <summary>
        /// Reads all frames from the WAV file.
        /// </summary>
        /// <returns>Array of interleaved samples (float values -1.0 to 1.0).</returns>
        public float[] ReadAll()
        {
            return ReadFrames((int)TotalFrames);
        }

        /// <summary>
        /// Reads a specified number of frames from the WAV file.
        /// </summary>
        /// <param name="count">Number of frames to read.</param>
        /// <returns>Array of interleaved samples (float values -1.0 to 1.0).</returns>
        public float[] ReadFrames(int count)
        {
            if (count <= 0 || _disposed || Position >= TotalFrames)
                return new float[0];

            int frames = (int)Math.Min(count, TotalFrames - Position);
            var samples = new float[frames * Channels];
            ReadFramesInto(samples, count);
            return samples;
        }

        /// <summary>
        /// Reads up to <paramref name="count"/> frames directly into
        /// <paramref name="dest"/> (which must hold at least
        /// frames*Channels samples). Zero-allocation chunked decode.
        /// Returns frames actually read.
        /// </summary>
        public int ReadFramesInto(float[] dest, int count)
        {
            if (count <= 0 || _disposed || Position >= TotalFrames || dest.Length < Channels)
                return 0;

            int framesToRead = (int)Math.Min(count, TotalFrames - Position);
            int samplesToRead = framesToRead * Channels;
            if (dest.Length < samplesToRead) return 0;

            long bytesToRead = framesToRead * Channels * (BitsPerSample / 8);
            
            // Ensure we're at the correct position in the data chunk
            long targetPosition = _dataStartPosition + (Position * Channels * (BitsPerSample / 8));
            if (_stream.Position != targetPosition)
            {
                _stream.Seek(targetPosition, SeekOrigin.Begin);
            }

            // Read based on bit depth
            if (BitsPerSample == 8)
            {
                for (int i = 0; i < samplesToRead; i++)
                {
                    byte sample = _reader.ReadByte();
                    dest[i] = (sample / 127.5f) - 1.0f; // Convert 0-255 to -1.0 to 1.0
                }
            }
            else if (BitsPerSample == 16)
            {
                for (int i = 0; i < samplesToRead; i++)
                {
                    short sample = _reader.ReadInt16();
                    dest[i] = sample / 32768.0f; // Convert -32768 to 32767 to -1.0 to 1.0
                }
            }
            else if (BitsPerSample == 24)
            {
                for (int i = 0; i < samplesToRead; i++)
                {
                    int sample = Read24BitInt();
                    dest[i] = sample / 8388608.0f; // Convert -8388608 to 8388607 to -1.0 to 1.0
                }
            }
            else if (BitsPerSample == 32 && _isFloat)
            {
                for (int i = 0; i < samplesToRead; i++)
                    dest[i] = _reader.ReadSingle();
            }
            else if (BitsPerSample == 32)
            {
                for (int i = 0; i < samplesToRead; i++)
                {
                    int sample = _reader.ReadInt32();
                    dest[i] = sample / 2147483648.0f; // Convert to -1.0 to 1.0
                }
            }
            else
            {
                throw new NotSupportedException($"Bits per sample {BitsPerSample} not supported.");
            }

            Position += framesToRead;
            return framesToRead;
        }

        /// <summary>
        /// Seeks to a specific frame position.
        /// </summary>
        /// <param name="frame">Frame position.</param>
        public void Seek(long frame)
        {
            if (frame < 0 || frame > TotalFrames)
                throw new ArgumentOutOfRangeException(nameof(frame), "Frame position out of range.");

            Position = frame;
        }

        /// <summary>
        /// Disposes the WavReader and releases resources.
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

        private void ReadHeader()
        {
            // Read RIFF header
            string riffHeader = Encoding.ASCII.GetString(_reader.ReadBytes(4));
            if (riffHeader != "RIFF")
                throw new InvalidDataException("Invalid WAV file: Missing RIFF header.");

            uint fileSize = _reader.ReadUInt32();

            string waveHeader = Encoding.ASCII.GetString(_reader.ReadBytes(4));
            if (waveHeader != "WAVE")
                throw new InvalidDataException("Invalid WAV file: Missing WAVE header.");

            // Read chunks
            bool foundFmt = false;
            bool foundData = false;

            while (!foundData)
            {
                string chunkId = Encoding.ASCII.GetString(_reader.ReadBytes(4));
                uint chunkSize = _reader.ReadUInt32();

                if (chunkId == "fmt ")
                {
                    ReadFormatChunk(chunkSize);
                    foundFmt = true;
                }
                else if (chunkId == "data")
                {
                    _dataStartPosition = _stream.Position;
                    _dataLength = chunkSize;
                    foundData = true;
                }
                else
                {
                    // Skip unknown chunk
                    _stream.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            if (!foundFmt)
                throw new InvalidDataException("Invalid WAV file: Missing format chunk.");

            TotalFrames = _dataLength / (Channels * (BitsPerSample / 8));
        }

        private void ReadFormatChunk(uint chunkSize)
        {
            ushort audioFormat = _reader.ReadUInt16();
            Channels = _reader.ReadUInt16();
            SampleRate = _reader.ReadInt32();
            uint byteRate = _reader.ReadUInt32();
            ushort blockAlign = _reader.ReadUInt16();
            BitsPerSample = _reader.ReadUInt16();

            if (audioFormat == 1) // PCM integer
            {
                _isFloat = false;
            }
            else if (audioFormat == 3) // IEEE float
            {
                _isFloat = true;
                if (BitsPerSample != 32)
                    throw new NotSupportedException("Only 32-bit IEEE float WAV is supported.");
            }
            else
            {
                throw new NotSupportedException($"WAV format tag {audioFormat} not supported (PCM or IEEE float only).");
            }

            // Skip any extra format data
            if (chunkSize > 16)
            {
                _stream.Seek(chunkSize - 16, SeekOrigin.Current);
            }
        }

        private int Read24BitInt()
        {
            byte b1 = _reader.ReadByte();
            byte b2 = _reader.ReadByte();
            byte b3 = _reader.ReadByte();

            // Sign-extend the 24-bit value to 32-bit
            int value = (b3 << 24) | (b2 << 16) | (b1 << 8);
            return value >> 8; // Sign extend
        }
    }
}
