using System;
using System.Collections.Generic;

namespace HelixAudio.Mixing
{
    public class MultiTrackMixer : MixerBase
    {
        private readonly List<Track> _tracks;

        private class Track
        {
            public float[] Buffer { get; set; }
            public float Volume { get; set; }

            public Track(float[] buffer, float volume)
            {
                Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
                Volume = Math.Max(0.0f, volume);
            }
        }

        public int TrackCount => _tracks.Count;

        public MultiTrackMixer(int sampleRate, int channels)
            : base(sampleRate, channels)
        {
            _tracks = new List<Track>();
        }

        public void AddTrack(float[] buffer, float volume = 1.0f)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            ValidateInput(buffer);
            _tracks.Add(new Track(buffer, Math.Max(0.0f, volume)));
        }

        public void SetVolume(int index, float volume)
        {
            if (index < 0 || index >= _tracks.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Track index out of range.");

            _tracks[index].Volume = Math.Max(0.0f, volume);
        }

        public void RemoveTrack(int index)
        {
            if (index < 0 || index >= _tracks.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Track index out of range.");

            _tracks.RemoveAt(index);
        }

        public void ClearTracks()
        {
            _tracks.Clear();
        }

        public float[] GetTrackBuffer(int index)
        {
            if (index < 0 || index >= _tracks.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Track index out of range.");

            float[] buffer = _tracks[index].Buffer;
            float[] copy = new float[buffer.Length];
            Array.Copy(buffer, copy, buffer.Length);
            return copy;
        }

        public float GetVolume(int index)
        {
            if (index < 0 || index >= _tracks.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Track index out of range.");

            return _tracks[index].Volume;
        }

        public override float[] Mix(float[] a, float[] b)
        {
            return MixAllTracks();
        }

        public float[] MixAllTracks()
        {
            if (_tracks.Count == 0)
                return new float[0];

            int maxLength = 0;
            foreach (var track in _tracks)
            {
                if (track.Buffer.Length > maxLength)
                    maxLength = track.Buffer.Length;
            }

            float[] output = new float[maxLength];

            foreach (var track in _tracks)
            {
                float volume = track.Volume;
                float[] buffer = track.Buffer;

                for (int i = 0; i < buffer.Length; i++)
                {
                    output[i] += buffer[i] * volume;
                }
            }

            for (int i = 0; i < output.Length; i++)
            {
                output[i] = ClampSample(output[i]);
            }

            return output;
        }

        public float[] MixAllTracks(int sampleCount)
        {
            if (_tracks.Count == 0 || sampleCount <= 0)
                return new float[0];

            float[] output = new float[sampleCount];

            foreach (var track in _tracks)
            {
                float volume = track.Volume;
                float[] buffer = track.Buffer;
                int samplesToMix = Math.Min(buffer.Length, sampleCount);

                for (int i = 0; i < samplesToMix; i++)
                {
                    output[i] += buffer[i] * volume;
                }
            }

            for (int i = 0; i < output.Length; i++)
            {
                output[i] = ClampSample(output[i]);
            }

            return output;
        }
    }
}
