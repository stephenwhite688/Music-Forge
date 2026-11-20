using HelixAudio.Core;

namespace HelixAudio.PluginHost.VST3;

public sealed class VST3Host : IDisposable
{
    private List<IAudioPlugin> _plugins = new();
    private bool _disposed;

    public IAudioPlugin? LoadPlugin(string path)
    {
        throw new NotImplementedException("VST3 plugin hosting requires external library implementation");
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

