namespace HelixAudio.Core
{
    /// <summary>
    /// Partial class representing an audio device (input or output).
    /// Contains metadata about the device including name, ID, supported formats, and latency profile.
    /// </summary>
    public partial class AudioDevice
    {
        /// <summary>
        /// Gets or sets the display name of the device (e.g., "Built-in Microphone", "Speakers").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier for the device.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this is the system default device.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Gets or sets the list of audio formats supported by this device.
        /// </summary>
        public System.Collections.Generic.List<AudioFormat> SupportedFormats { get; set; } =
            new System.Collections.Generic.List<AudioFormat>();

        /// <summary>
        /// Gets or sets the latency profile for this device (Low, Medium, or High).
        /// </summary>
        public LatencyProfile LatencyProfile { get; set; } = LatencyProfile.Medium;

        /// <summary>
        /// Gets or sets the direction of the device (Input for capture devices, Output for playback devices).
        /// </summary>
        public DeviceDirection Direction { get; set; } = DeviceDirection.Output;
    }
}