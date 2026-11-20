using System;
using System.Threading;

namespace HelixAudio.Backend.Wasapi
{
    public class WasapiOutputThread : IDisposable
    {
        private readonly string _deviceId;
        private readonly CircularBuffer _buffer;
        private readonly int _sampleRate;
        private readonly int _channels;
        private Thread? _thread;
        private bool _running;
        private bool _paused;
        private readonly object _lock;
        private int _framesPerBuffer;
        private bool _disposed;

        public WasapiOutputThread(string deviceId, CircularBuffer buffer, int sampleRate, int channels)
        {
            _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _sampleRate = sampleRate;
            _channels = channels;
            _lock = new object();
            _running = false;
            _paused = false;
            _disposed = false;
            _framesPerBuffer = (int)(sampleRate * 0.01);
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_running)
                    return;

                _running = true;
                _paused = false;
                _thread = new Thread(ThreadLoop)
                {
                    Name = "WASAPI Output Thread",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _thread.Start();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _running = false;
                _paused = false;
                _thread?.Join(1000);
                _thread = null;
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                _paused = true;
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                _paused = false;
            }
        }

        private void ThreadLoop()
        {
            int samplesPerBuffer = _framesPerBuffer * _channels;
            float[] buffer = new float[samplesPerBuffer];

            while (_running)
            {
                if (_paused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                int samplesRead = _buffer.Read(buffer, samplesPerBuffer);

                if (samplesRead < samplesPerBuffer)
                {
                    Array.Clear(buffer, samplesRead, samplesPerBuffer - samplesRead);
                }

                Thread.Sleep((int)(_framesPerBuffer * 1000.0 / _sampleRate));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}

