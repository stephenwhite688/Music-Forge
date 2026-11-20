namespace HelixAudio.Core
{
    /// <summary>
    /// Represents different sample data types for audio processing.
    /// </summary>
    public enum SampleType
    {
        /// <summary>
        /// 32-bit floating point samples (range: -1.0 to 1.0). Default for DSP processing.
        /// </summary>
        Float32,

        /// <summary>
        /// 16-bit signed integer samples (range: -32768 to 32767). Common for WAV files.
        /// </summary>
        Int16,

        /// <summary>
        /// 24-bit signed integer samples (range: -8388608 to 8388607). Used in high-quality audio.
        /// </summary>
        Int24,

        /// <summary>
        /// 32-bit signed integer samples (range: -2147483648 to 2147483647). Rarely used due to size.
        /// </summary>
        Int32
    }
}