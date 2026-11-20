using HelixAudio.Core;
using System;
using System.Collections.Generic;

namespace HelixAudio.PluginHost;

public class PluginNode : AudioNode
{
    private readonly IAudioPlugin _plugin;
    private bool _initialized;

    public IAudioPlugin Plugin => _plugin;

    public PluginNode(int sampleRate, int channels, IAudioPlugin plugin, string name = null)
        : base(sampleRate, channels, name ?? plugin?.Name ?? "PluginNode")
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _plugin.Initialize(sampleRate, channels);
        _initialized = true;
    }

    public override float[] Process(float[] input)
    {
        if (!_initialized)
            return input;

        try
        {
            return _plugin.Process(input);
        }
        catch
        {
            return input;
        }
    }

    public override void Reset()
    {
        try { _plugin.Reset(); } catch { }
    }

    public Dictionary<string, float> GetParameters()
    {
        try { return _plugin.GetParameters(); }
        catch { return new Dictionary<string, float>(); }
    }

    public void SetParameter(string name, float value)
    {
        try { _plugin.SetParameter(name, value); } catch { }
    }
}
