using System;

namespace HelixAudio.Core
{
    /// <summary>
    /// Provides timing and sample position tracking for the audio engine.
    /// </summary>
    public class AudioClock : IDisposable
    {
        private long _samplePosition;
        private DateTime _startTime;
        private int _sampleRate;

        /// <summary>
        /// Gets the current sample position in the audio buffer.
        /// </summary>
        public long SamplePosition => _samplePosition;

        /// <summary>
        /// Gets the elapsed time in seconds since playback started.
        /// </summary>
        public double Seconds => (double)_samplePosition / _sampleRate;

        /// <summary>
        /// Gets the audio sample rate in Hz.
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Initializes a new instance of the AudioClock class.
        /// </summary>
        /// <param name="sampleRate">The audio sample rate in Hz (e.g., 44100).</param>
        public AudioClock(int sampleRate)
        {
            _sampleRate = sampleRate;
            SampleRate = _sampleRate;
            Reset();
        }

        /// <summary>
        /// Advances the sample position by the specified number of frames.
        /// </summary>
        /// <param name="frames">The number of frames to advance.</param>
        public void Advance(int frames)
        {
            _samplePosition += frames;
        }

        /// <summary>
        /// Advances the sample position by the specified number of seconds.
        /// </summary>
        /// <param name="seconds">The number of seconds to advance.</param>
        public void AdvanceSeconds(double seconds)
        {
            _samplePosition += (long)(seconds * _sampleRate);
        }

        /// <summary>
        /// Resets the clock to the start position.
        /// </summary>
        public void Reset()
        {
            _samplePosition = 0;
            _startTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the current time including elapsed playback time.
        /// </summary>
        /// <returns>The current UTC datetime.</returns>
        public DateTime GetTime()
        {
            return _startTime.AddSeconds(Seconds);
        }

        /// <summary>
        /// Gets the elapsed playback time as a TimeSpan.
        /// </summary>
        /// <returns>The elapsed time since playback started.</returns>
        public TimeSpan GetElapsed()
        {
            return TimeSpan.FromSeconds(Seconds);
        }

        /// <summary>
        /// Gets the sample position at the specified datetime.
        /// </summary>
        /// <param name="time">The datetime to calculate the sample position for.</param>
        /// <returns>The sample position at that time.</returns>
        public long GetSamplePositionAt(DateTime time)
        {
            return (long)((time - _startTime).TotalSeconds * _sampleRate);
        }

        /// <summary>
        /// Synchronizes the clock to a specific sample position.
        /// </summary>
        /// <param name="samplePosition">The sample position to synchronize to.</param>
        public void Synchronize(long samplePosition)
        {
            _samplePosition = samplePosition;
        }

        /// <summary>
        /// Releases resources used by the AudioClock.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}