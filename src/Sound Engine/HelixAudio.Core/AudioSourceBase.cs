using System;

namespace HelixAudio.Core
{
    public abstract class AudioSourceBase : IAudioNode, IAudioSource
    {
        public string Name { get; set; }
        public int SampleRate { get; protected set; }
        public int Channels { get; protected set; }

        public AudioFormat Format { get; protected set; }

        protected AudioSourceBase(int sampleRate, int channels, string name)
        {
            SampleRate = sampleRate;
            Channels = channels;
            Name = name ?? GetType().Name;
        }

        public virtual void Prepare(AudioFormat format)
        {
            Format = format;
        }

        public abstract float[] Read(int frames);

        public virtual float[] Process(float[] buffer)
        {
            int frames = (buffer?.Length ?? 0) / Channels;
            return Read(frames);
        }

        public virtual void Reset() { }
    }
}
