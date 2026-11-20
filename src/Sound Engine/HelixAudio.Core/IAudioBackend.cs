namespace HelixAudio.Core
{
    public interface IAudioBackend
    {
        string Name { get; }
        bool IsInitialized { get; }
        bool IsRunning { get; }
        BackendOptions Options { get; }
        AudioFormat NegotiatedFormat { get; }

        void Initialize(BackendOptions options);
        void Start();
        void Stop();
        void Pause();
        void Resume();
        void Write(float[] buffer);
        float[] Read(int frames);
        void Close();
    }
}
