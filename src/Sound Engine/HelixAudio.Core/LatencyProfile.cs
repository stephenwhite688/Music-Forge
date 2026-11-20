namespace HelixAudio.Core
{
    /// <summary>
    /// Represents different latency profiles for audio processing and playback.
    /// Lower latency reduces delay but increases CPU usage; higher latency does the opposite.
    /// </summary>
    public enum LatencyProfile
    {
        /// <summary>
        /// Low latency profile: minimal delay, suitable for real-time monitoring and interactive applications.
        /// Uses smaller buffer sizes at the cost of higher CPU usage.
        /// </summary>
        Low,

        /// <summary>
        /// Medium latency profile: balanced trade-off between delay and CPU usage.
        /// Suitable for most general-purpose audio applications (default choice).
        /// </summary>
        Medium,

        /// <summary>
        /// High latency profile: maximum delay reduction, suitable for offline processing or high-quality rendering.
        /// Uses larger buffer sizes for better performance at the cost of increased latency.
        /// </summary>
        High
    }
}