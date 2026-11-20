using HelixAudio.Core;
using System;
using System.Collections.Generic;

namespace HelixAudio.MIDI
{
    /// <summary>
    /// Represents a software synthesizer that can play MIDI notes with sample-based waveforms.
    /// </summary>
    public class Synthesizer : IDisposable
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private bool _disposed;
        private Dictionary<int, bool> _activeNotes;

        /// <summary>
        /// Gets the audio format of the synthesizer.
        /// </summary>
        public AudioFormat Format { get; private set; }

        /// <summary>
        /// Creates a new synthesizer with the specified sample rate and channels.
        /// </summary>
        /// <param name="sampleRate">The audio sample rate in Hz (e.g., 44100, 48000).</param>
        /// <param name="channels">The number of audio channels (1 for mono, 2 for stereo).</param>
        public Synthesizer(int sampleRate, int channels)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _activeNotes = new Dictionary<int, bool>();

            Format = new AudioFormat(
                sampleRate,
                channels,
                true,   // float samples
                32      // bits per sample
            );
        }

        /// <summary>
        /// Triggers a note on with the specified parameters.
        /// </summary>
        /// <param name="channel">The MIDI channel (0-15).</param>
        /// <param name="note">The note number (MIDI pitch, 0-127).</param>
        /// <param name="velocity">The velocity of the note (0-127).</param>
        public void NoteOn(int channel, int note, int velocity)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentException("Channel must be between 0 and 15.", nameof(channel));
            if (note < 0 || note > 127)
                throw new ArgumentException("Note must be between 0 and 127.", nameof(note));
            if (velocity < 0 || velocity > 127)
                throw new ArgumentException("Velocity must be between 0 and 127.", nameof(velocity));

            _activeNotes[note] = true;
        }

        /// <summary>
        /// Triggers a note off for the specified note.
        /// </summary>
        /// <param name="channel">The MIDI channel (0-15).</param>
        /// <param name="note">The note number (MIDI pitch, 0-127).</param>
        public void NoteOff(int channel, int note)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentException("Channel must be between 0 and 15.", nameof(channel));
            if (note < 0 || note > 127)
                throw new ArgumentException("Note must be between 0 and 127.", nameof(note));

            _activeNotes[note] = false;
        }

        /// <summary>
        /// Stops all active notes in the synthesizer.
        /// </summary>
        public void AllNotesOff()
        {
            _activeNotes.Clear();
        }

        /// <summary>
        /// Releases all resources used by the Synthesizer object.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _activeNotes?.Clear();
            }
        }
    }
}