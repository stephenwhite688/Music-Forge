using HelixAudio.Core;

namespace HelixAudio.Backend.Wasapi;

public sealed class WasapiCapture : IDisposable
{
    public AudioFormat Format { get; private set; }
    private bool _disposed;

    public WasapiCapture(WASAPIBackend backend)
    {
        Format = new AudioFormat(
            backend.Options.SampleRate,
            backend.Options.Channels,
            true,       // float samples
            32          // bits per sample
        );
    }

    public void Start(AudioCallback callback)
    {
        // Capture not implemented yet
    }

    public void Stop()
    {
        // Capture not implemented yet
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
