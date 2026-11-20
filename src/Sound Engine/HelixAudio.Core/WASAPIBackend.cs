using System;

namespace HelixAudio.Core
{
    public class WASAPIBackend : IAudioBackend
    {
        private BackendOptions _options;
        private bool _init;
        private bool _run;
        private AudioFormat _negotiatedFormat;

        public string Name => "WASAPI";
        public bool IsInitialized => _init;
        public bool IsRunning => _run;
        public BackendOptions Options => _options;
        public AudioFormat NegotiatedFormat => _negotiatedFormat;

        public void Initialize(BackendOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.SampleRate <= 0) throw new ArgumentException("Sample rate must be positive.");
            if (_options.Channels <= 0) throw new ArgumentException("Channel count must be positive.");
            if (_options.BufferSize <= 0) throw new ArgumentException("Buffer size must be positive.");

            // FIXED: AudioFormat is immutable, so use constructor instead of object initializer
            _negotiatedFormat = new AudioFormat(
                _options.SampleRate,
                _options.Channels,
                true,
                32
            );

            _init = true;
        }

        public void Start()
        {
            if (!_init) throw new InvalidOperationException("Backend not initialized.");
            if (_run) return;
            _run = true;
        }

        public void Stop()
        {
            if (!_run) return;
            _run = false;
        }

        public void Pause()
        {
            if (!_run) return;
        }

        public void Resume()
        {
            if (!_run) return;
        }

        public void Write(float[] buffer)
        {
            if (!_run) throw new InvalidOperationException("Backend not running.");
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        }

        public float[] Read(int sampleCount)
        {
            if (!_run) throw new InvalidOperationException("Backend not running.");
            if (sampleCount <= 0) return Array.Empty<float>();
            return new float[sampleCount];
        }

        public void Close()
        {
            Stop();
            _init = false;
        }

        private int ConvertBufferSize(uint bufferSize) => (int)bufferSize;
        private int ConvertFrameCount(uint frameCount) => (int)frameCount;
    }
}
