using System;

namespace HelixAudio.Backend.Wasapi
{
    public class CircularBuffer
    {
        private readonly float[] _buffer;
        private int _writePos;
        private int _readPos;
        private int _count;
        private readonly object _lock;
        private readonly int _capacity;

        public int Capacity => _capacity;

        public int AvailableRead
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        public int AvailableWrite
        {
            get
            {
                lock (_lock)
                {
                    return _capacity - _count;
                }
            }
        }

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be positive.", nameof(capacity));

            _capacity = capacity;
            _buffer = new float[capacity];
            _writePos = 0;
            _readPos = 0;
            _count = 0;
            _lock = new object();
        }

        public int Write(float[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            lock (_lock)
            {
                int samplesToWrite = Math.Min(data.Length, _capacity - _count);
                if (samplesToWrite == 0)
                    return 0;

                int firstChunk = Math.Min(samplesToWrite, _capacity - _writePos);
                int secondChunk = samplesToWrite - firstChunk;

                Array.Copy(data, 0, _buffer, _writePos, firstChunk);
                _writePos = (_writePos + firstChunk) % _capacity;

                if (secondChunk > 0)
                {
                    Array.Copy(data, firstChunk, _buffer, 0, secondChunk);
                    _writePos = secondChunk;
                }

                _count += samplesToWrite;
                return samplesToWrite;
            }
        }

        public int Read(float[] target, int count)
        {
            if (target == null || target.Length == 0 || count <= 0)
                return 0;

            lock (_lock)
            {
                int samplesToRead = Math.Min(count, _count);
                samplesToRead = Math.Min(samplesToRead, target.Length);

                if (samplesToRead == 0)
                    return 0;

                int firstChunk = Math.Min(samplesToRead, _capacity - _readPos);
                int secondChunk = samplesToRead - firstChunk;

                Array.Copy(_buffer, _readPos, target, 0, firstChunk);
                _readPos = (_readPos + firstChunk) % _capacity;

                if (secondChunk > 0)
                {
                    Array.Copy(_buffer, 0, target, firstChunk, secondChunk);
                    _readPos = secondChunk;
                }

                _count -= samplesToRead;
                return samplesToRead;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _capacity);
                _writePos = 0;
                _readPos = 0;
                _count = 0;
            }
        }

        public bool IsEmpty()
        {
            lock (_lock)
            {
                return _count == 0;
            }
        }

        public bool IsFull()
        {
            lock (_lock)
            {
                return _count == _capacity;
            }
        }
    }
}
