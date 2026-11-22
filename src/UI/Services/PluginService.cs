using HelixAudio.Core;
using HelixAudio.PluginHost.CLAP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicForge.UI.Services;

/// <summary>A loaded CLAP plugin in the chain.</summary>
public sealed class PluginEntry
{
    public required ClapPluginNode Node;
    public required string Path;
    public bool Enabled = true;
}

/// <summary>
/// Owns the CLAP plugin instances inserted into the playback chain.
/// Enabled plugins are appended after the EQ in
/// <see cref="MeterService.Chain"/> so they process the same stream as
/// everything else.
/// </summary>
public sealed class PluginService
{
    public static PluginService Instance { get; } = new PluginService();

    private readonly List<PluginEntry> _plugins = new();
    private readonly object _gate = new();

    /// <summary>All loaded plugins (enabled or not), in insert order.</summary>
    public IReadOnlyList<PluginEntry> Plugins
    {
        get { lock (_gate) return _plugins.ToArray(); }
    }

    /// <summary>Enabled plugin nodes for the playback chain.</summary>
    public IEnumerable<IAudioNode> ActiveNodes
    {
        get { lock (_gate) return _plugins.Where(p => p.Enabled).Select(p => (IAudioNode)p.Node).ToArray(); }
    }

    /// <summary>Loads a .clap file and appends it to the chain. Returns the entry.</summary>
    public PluginEntry AddPlugin(string clapPath)
    {
        var node = ClapPluginNode.Load(clapPath);
        var entry = new PluginEntry { Node = node, Path = clapPath };
        lock (_gate) _plugins.Add(entry);
        return entry;
    }

    public void SetEnabled(PluginEntry entry, bool enabled)
    {
        lock (_gate) entry.Enabled = enabled;
    }

    public void RemovePlugin(PluginEntry entry)
    {
        lock (_gate)
        {
            if (_plugins.Remove(entry)) entry.Node.Dispose();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            foreach (var p in _plugins) p.Node.Dispose();
            _plugins.Clear();
        }
    }
}
