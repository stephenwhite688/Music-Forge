using System;

namespace HelixAudio.Core
{
    /// <summary>
    /// Interface representing an audio source that provides audio data to the engine.
    /// Sources supply audio buffers for playback, processing, or streaming.
    /// </summary>
    public interface IAudioSource
    {
        /// <summary>
        /// Reads audio frames from the source and returns them as a buffer.
        /// Implementers should provide data up to `frames` count or return an empty array if no data is available.
        /// </summary>
        /// <param name="frames">The maximum number of frames to read.</param>
        /// <returns>An audio buffer containing the read data, or an empty array if no data is available.</returns>
        float[] Read(int frames);
    }
}