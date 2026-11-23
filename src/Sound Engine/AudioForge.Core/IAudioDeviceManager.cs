namespace AudioForge.Core
{
    public interface IAudioDeviceManager
    {
        int SampleRate { get; }
        int Channels { get; }
        string CurrentOutputDeviceId { get; }
        string CurrentInputDeviceId { get; }

        void Initialize(int sampleRate, int channels);
        void Start();
        void Stop();
        void Pause();
        void Resume();
        void Write(float[] buffer);
        float[] Read(int frames);
    }
}
