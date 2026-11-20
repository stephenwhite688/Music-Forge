namespace HelixAudio.Core
{
    /// <summary>
    /// Configuration options for the audio backend system.
    /// Contains settings for device selection, sample rate, buffer size, and latency profile.
    /// </summary>
    public class BackendOptions
    {
        /// <summary>
        /// Gets or sets the device ID to use (null for default device).
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the sample rate in Hz. Default is 44100 Hz.
        /// </summary>
        public int SampleRate { get; set; } = 44100;

        /// <summary>
        /// Gets or sets the number of audio channels. Default is 2 (stereo).
        /// </summary>
        public int Channels { get; set; } = 2;

        /// <summary>
        /// Gets or sets the buffer size in frames. Larger buffers reduce CPU usage but increase latency.
        /// Default is 1024 frames.
        /// </summary>
        public int BufferSize { get; set; } = 1024;

        /// <summary>
        /// Gets or sets the latency profile preference.
        /// </summary>
        public LatencyProfile Latency { get; set; } = LatencyProfile.Medium;

        /// <summary>
        /// Creates default BackendOptions with standard settings.
        /// </summary>
        /// <returns>Default BackendOptions configuration.</returns>
        public static BackendOptions CreateDefault() => new BackendOptions();

        /// <summary>
        /// Creates BackendOptions optimized for low latency applications (e.g., real-time monitoring).
        /// Uses higher sample rate and smaller buffer size at the cost of CPU usage.
        /// </summary>
        /// <returns>BackendOptions configuration with low latency profile.</returns>
        public static BackendOptions CreateLowLatency() => new BackendOptions
        {
            SampleRate = 48000,
            BufferSize = 256,
            Latency = LatencyProfile.Low
        };

        /// <summary>
        /// Creates BackendOptions optimized for high quality audio processing.
        /// Uses higher sample rate and larger buffer size for better audio fidelity at the cost of latency.
        /// </summary>
        /// <returns>BackendOptions configuration with high quality profile.</returns>
        public static BackendOptions CreateHighQuality() => new BackendOptions
        {
            SampleRate = 96000,
            BufferSize = 2048,
            Latency = LatencyProfile.High
        };
    }
}