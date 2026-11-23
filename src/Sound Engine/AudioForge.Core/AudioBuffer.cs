using System;

namespace AudioForge.Core
{
    /// <summary>
    /// Represents a block of audio data containing interleaved samples for multiple channels.
    /// Used throughout the audio processing pipeline to store and pass audio buffers between nodes.
    /// </summary>
    public class AudioBuffer
    {
        /// <summary>
        /// Gets or sets the array of audio samples (interleaved, float-point).
        /// Length equals Frames × Channels.
        /// </summary>
        public float[] Data { get; set; }

        /// <summary>
        /// Gets the number of frames in this buffer (each frame contains one sample per channel).
        /// </summary>
        public int Frames { get; private set; }

        /// <summary>
        /// Gets the number of audio channels.
        /// </summary>
        public int Channels { get; private set; }

        /// <summary>
        /// Gets the total number of samples in the buffer (Frames × Channels).
        /// </summary>
        public int Length => Frames * Channels;

        /// <summary>
        /// Initializes a new instance of the AudioBuffer class with the specified size.
        /// Allocates a zero-initialized float array for the data.
        /// </summary>
        /// <param name="frames">The number of frames (each containing one sample per channel).</param>
        /// <param name="channels">The number of audio channels.</param>
        /// <exception cref="ArgumentException">Thrown when frames or channels are less than or equal to zero.</exception>
        public AudioBuffer(int frames, int channels)
        {
            if (frames <= 0) throw new ArgumentException("Frames must be positive.", nameof(frames));
            if (channels <= 0) throw new ArgumentException("Channels must be positive.", nameof(channels));

            Frames = frames;
            Channels = channels;
            Data = new float[frames * channels];
        }

        /// <summary>
        /// Clears all data in the buffer by setting it to zeros.
        /// </summary>
        public void Clear()
        {
            Array.Clear(Data, 0, Data.Length);
        }
    }
}