using System;
using System.IO;

namespace AudioForge.IO
{
    /// <summary>
    /// Represents an audio stream with sample rate, channel count, and bit depth information.
    /// Provides methods for reading and writing audio frame data.
    /// </summary>
    public class AudioStream : IDisposable
    {
        private Stream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;
        private int _bitsPerSample;
        private readonly bool _floatPcm;
        private bool _disposed;
        private readonly byte[] _byteScratch = new byte[256 * 1024];
        private Func<float[], int, int>? _frameSource;
        private Func<long, bool>? _seekSource;
        private Action? _disposeCallback;

        /// <summary>
        /// Gets the sample rate of the audio stream in Hz.
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Gets the number of audio channels (e.g., 2 for stereo, 1 for mono).
        /// </summary>
        public int Channels { get; private set; }

        /// <summary>
        /// Gets the bits per sample of the audio data.
        /// </summary>
        public int BitsPerSample => _bitsPerSample;

        /// <summary>True when the stream stores IEEE float samples rather than integer PCM.</summary>
        public bool IsFloatPcm => _floatPcm;

        /// <summary>
        /// Gets the current position in the stream (number of frames from start).
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Gets the total number of frames in the audio.
        /// </summary>
        public long TotalFrames { get; set; }

        /// <summary>
        /// Initializes a new instance of the AudioStream class with default settings (44100 Hz, 2 channels, 16-bit).
        /// </summary>
        public AudioStream()
        {
            _stream = new MemoryStream();
            _bitsPerSample = 16;
            SampleRate = 44100;
            Channels = 2;
        }

        /// <summary>
        /// Initializes a new instance of the AudioStream class with specified stream, sample rate, channels, and bit depth.
        /// </summary>
        /// <param name="stream">The underlying stream to use for reading/writing audio data.</param>
        /// <param name="sampleRate">The sample rate in Hz (e.g., 44100, 48000).</param>
        /// <param name="channels">The number of audio channels (e.g., 1 for mono, 2 for stereo).</param>
        /// <param name="bitsPerSample">The bits per sample (typically 16 or 32).</param>
        public AudioStream(Stream stream, int sampleRate, int channels, int bitsPerSample = 16, bool floatPcm = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _floatPcm = floatPcm;
            SampleRate = sampleRate;
            Channels = channels;
            _bitsPerSample = bitsPerSample;
            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);
            Position = 0;
            TotalFrames = stream.Length / (channels * (bitsPerSample / 8));
        }

        /// <summary>
        /// Creates a read-only stream backed by an on-demand frame decoder
        /// (chunked decode - no whole-file buffering). The delegate writes up
        /// to <paramref name="count"/> frames of interleaved floats into
        /// <c>dest</c> and returns the frames actually produced.
        /// </summary>
        public static AudioStream CreateStreaming(
            int sampleRate, int channels, long totalFrames,
            Func<float[], int, int> readFrames,
            Func<long, bool>? seek = null,
            Action? onDispose = null)
        {
            var s = new AudioStream
            {
                _stream = Stream.Null,
                _bitsPerSample = 32,
                SampleRate = sampleRate,
                Channels = channels,
                TotalFrames = totalFrames
            };
            s._frameSource = readFrames;
            s._seekSource = seek;
            s._disposeCallback = onDispose;
            return s;
        }

        /// <summary>
        /// Attempts a sample-accurate seek to <paramref name="frame"/>.
        /// Returns false when the backing source cannot seek (e.g. live
        /// decode streams).
        /// </summary>
        public bool TrySeek(long frame)
        {
            if (_disposed || frame < 0 || frame > TotalFrames) return false;
            if (_frameSource is not null)
            {
                if (_seekSource is null || !_seekSource(frame)) return false;
                Position = frame;
                return true;
            }
            if (!_stream.CanSeek) return false;
            int bytesPerSample = _floatPcm ? 4 : Math.Max(1, _bitsPerSample / 8);
            _stream.Position = frame * Channels * bytesPerSample;
            Position = frame;
            return true;
        }

        /// <summary>Reads up to count frames directly into dest starting at index 0. Returns frames read.</summary>
        public int ReadFramesInto(float[] dest, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioStream));
            if (_frameSource is not null)
            {
                int f = _frameSource(dest, count);
                Position += f;
                return f;
            }
            if (count <= 0 || dest.Length < count * Channels) return 0;

            int maxSamples = count * Channels;
            if (_floatPcm)
            {
                int floatsRead = 0;
                while (floatsRead < maxSamples)
                {
                    int chunk = Math.Min(maxSamples - floatsRead, _byteScratch.Length / 4);
                    int got = _stream.Read(_byteScratch, 0, chunk * 4);
                    if (got <= 0) break;
                    int cf = got / 4;
                    for (int i = 0; i < cf; i++)
                        dest[floatsRead + i] = BitConverter.ToSingle(_byteScratch, i * 4);
                    floatsRead += cf;
                    if (got < chunk * 4) break;
                }

                Position += floatsRead / Channels;
                return floatsRead / Channels;
            }

            int bytesPerSample = _bitsPerSample / 8;
            int samplesRead = 0;
            while (samplesRead < maxSamples)
            {
                int chunkSamples = Math.Min(maxSamples - samplesRead, _byteScratch.Length / bytesPerSample);
                int bytesWanted = chunkSamples * bytesPerSample;
                int got = _stream.Read(_byteScratch, 0, bytesWanted);
                if (got <= 0) break;
                int chunkGot = got / bytesPerSample;
                for (int i = 0; i < chunkGot; i++)
                    dest[samplesRead + i] = DecodeSample(_byteScratch, i * bytesPerSample);
                samplesRead += chunkGot;
                if (got < bytesWanted) break;
            }
            Position += samplesRead / Channels;
            return samplesRead / Channels;
        }

        private float DecodeSample(byte[] b, int off)
        {
            if (_bitsPerSample == 16) return BitConverter.ToInt16(b, off) / 32768.0f;
            if (_bitsPerSample == 32) return BitConverter.ToInt32(b, off) / 2147483648.0f;
            return 0f;
        }

        public float[] ReadFrames(int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioStream));
            if (_frameSource is not null)
            {
                if (count <= 0) return new float[0];
                var buf = new float[count * Channels];
                int f = _frameSource(buf, count);
                if (f < count) Array.Resize(ref buf, f * Channels);
                Position += f;
                return buf;
            }
            if (count <= 0) return new float[0];

            if (_floatPcm)
            {
                int wanted = count * Channels;
                var fbuf = new byte[wanted * 4];
                int fgot = _stream.Read(fbuf, 0, fbuf.Length);
                int fcount = fgot / 4;
                var fres = new float[fcount];
                for (int i = 0; i < fcount; i++) fres[i] = BitConverter.ToSingle(fbuf, i * 4);
                Position += fcount / Channels;
                return fres;
            }

            int bytesPerSample = _bitsPerSample / 8;
            int bytesToRead = count * Channels * bytesPerSample;
            byte[] buffer = new byte[bytesToRead];
            int bytesRead = _stream.Read(buffer, 0, bytesToRead);
            int samplesRead = bytesRead / bytesPerSample;
            float[] result = new float[samplesRead];

            if (_bitsPerSample == 16)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    short sample = BitConverter.ToInt16(buffer, i * 2);
                    result[i] = sample / 32768.0f;
                }
            }
            else if (_bitsPerSample == 32)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    int sample = BitConverter.ToInt32(buffer, i * 4);
                    result[i] = sample / 2147483648.0f;
                }
            }

            Position += samplesRead / Channels;
            return result;
        }

        /// <summary>
        /// Writes the specified audio frames to the stream and converts float values to the appropriate format.
        /// </summary>
        /// <param name="buffer">An array of float values representing audio samples to write.</param>
        public void WriteFrames(float[] buffer)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioStream));
            if (_frameSource is not null)
                throw new NotSupportedException("Streaming sources are read-only.");
            if (buffer == null || buffer.Length == 0) return;

            int bytesPerSample = _bitsPerSample / 8;
            byte[] byteBuffer = new byte[buffer.Length * bytesPerSample];

            if (_bitsPerSample == 16)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    short sample = (short)(buffer[i] * 32767.0f);
                    BitConverter.TryWriteBytes(new Span<byte>(byteBuffer, i * 2, 2), sample);
                }
            }
            else if (_bitsPerSample == 32)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    int sample = (int)(buffer[i] * 2147483647.0f);
                    BitConverter.TryWriteBytes(new Span<byte>(byteBuffer, i * 4, 4), sample);
                }
            }

            _stream.Write(byteBuffer, 0, byteBuffer.Length);
            Position += buffer.Length / Channels;
        }

        /// <summary>
        /// Releases all resources used by the AudioStream.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _stream?.Dispose();
                _disposed = true;
                _disposeCallback?.Invoke();
            }
        }
    }
}
