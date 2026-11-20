namespace HelixAudio.Core
{
    /// <summary>
    /// Specifies whether an audio device or component is an input (capture) or output (playback) device.
    /// </summary>
    public enum DeviceDirection
    {
        /// <summary>
        /// Represents an input device (microphone, line-in, etc.) that captures audio.
        /// </summary>
        Input,

        /// <summary>
        /// Represents an output device (speakers, headphones, etc.) that plays audio.
        /// </summary>
        Output
    }
}