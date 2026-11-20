using HelixAudio.Core;

namespace HelixAudio.Streaming;

public sealed class StreamManager : IDisposable
{
    private List<IStream> _streams = new();
    private bool _disposed;

    public void AddStream(IStream stream)
    {
        _streams.Add(stream);
    }

    public void RemoveStream(IStream stream)
    {
        stream.Dispose();
        _streams.Remove(stream);
    }

    public async Task PlayAllAsync()
    {
        foreach (var stream in _streams)
            await stream.PlayAsync();
    }

    public async Task StopAllAsync()
    {
        foreach (var stream in _streams)
            await stream.StopAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var stream in _streams)
            stream.Dispose();
        _streams.Clear();
    }
}
