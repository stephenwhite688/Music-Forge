using System;

namespace AudioForge.Core
{
    /// <summary>
    /// Delegate for audio buffer callbacks in the audio engine.
    /// </summary>
    /// <param name="buffer">The PCM audio buffer containing sample data.</param>
    public delegate void AudioCallback(float[] buffer);
}