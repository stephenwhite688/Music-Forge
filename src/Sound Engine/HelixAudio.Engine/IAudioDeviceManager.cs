namespace HelixAudio.Engine
{
    public interface IAudioDeviceManager
    {
        int SampleRate { get; }
        int Channels { get; }
        string CurrentOutputDeviceId { get; set; }
        string CurrentInputDeviceId { get; set; }

        void Initialize(int sampleRate, int channels);
        void Start();
        void Stop();
        void Pause();
        void Resume();
        void Write(float[] buffer);
        float[] Read(int sampleCount);
    }
}
