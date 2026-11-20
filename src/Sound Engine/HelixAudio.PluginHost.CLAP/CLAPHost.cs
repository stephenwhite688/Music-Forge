using HelixAudio.Core;

namespace HelixAudio.PluginHost.CLAP;

public sealed class CLAPHost : IDisposable
{
    private List<IAudioPlugin> _plugins = new();
    private bool _disposed;

    public IAudioPlugin? LoadPlugin(string path)
    {
        throw new NotImplementedException("CLAP plugin hosting requires external library implementation");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var plugin in _plugins)
            // plugin disposal handled by host;
        _plugins.Clear();
    }
}

