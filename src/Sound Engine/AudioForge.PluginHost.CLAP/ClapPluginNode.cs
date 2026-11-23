using AudioForge.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.PluginHost.CLAP;

/// <summary>
/// Adapts a loaded <see cref="ClapPlugin"/> to the AudioForge
/// <see cref="AudioNode"/> contract so it can sit in a realtime chain.
/// Requires the plugin's main audio ports to match the stream channel
/// count. Parameter values are addressed by stable clap_id; automation
/// maps parameter names to ids via <see cref="ParameterIds"/>.
/// </summary>
public sealed class ClapPluginNode : AudioNode, IDisposable
{
    private readonly ClapPlugin _plugin;
    private readonly Dictionary<string, uint> _paramIds = new(StringComparer.OrdinalIgnoreCase);
    private float[] _inScratch = Array.Empty<float>();
    private float[] _outScratch = Array.Empty<float>();
    private bool _ownsPlugin;

    public ClapPlugin Plugin => _plugin;
    public IReadOnlyDictionary<string, uint> ParameterIds => _paramIds;

    /// <summary>Wraps an already-loaded plugin (node does not dispose it).</summary>
    public ClapPluginNode(ClapPlugin plugin, int sampleRate, int channels, string? name = null)
        : base(sampleRate, channels, name ?? $"CLAP: {plugin.Name}")
    {
        _plugin = plugin;
        _ownsPlugin = false;
    }

    /// <summary>Loads a .clap file and wraps the first (or named) plugin.</summary>
    public static ClapPluginNode Load(string clapPath, string? pluginId = null,
        int sampleRate = 48000, int channels = 2, ClapPluginHost? host = null)
    {
        var h = host ?? new ClapPluginHost();
        var descs = h.EnumerateFile(clapPath);
        var desc = pluginId is null ? (descs.Count > 0 ? descs[0] : throw new InvalidOperationException("No plugins in file."))
            : descs.FirstOrDefault(d => d.Id == pluginId)
              ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found.");
        var plugin = h.CreatePlugin(desc);
        return new ClapPluginNode(plugin, sampleRate, channels) { _ownsPlugin = true };
    }

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        var (inCh, outCh) = _plugin.GetMainPortChannels();
        if (inCh != format.Channels || outCh != format.Channels)
            throw new InvalidOperationException(
                $"CLAP plugin '{_plugin.Name}' main ports are {inCh}->{outCh}, " +
                $"chain format is {format.Channels}ch. Channel-mismatched plugins are not supported yet.");
        _plugin.Activate(format.SampleRate, 1, 8192, inCh, outCh);
        _plugin.StartProcessing();
        IndexParameters();
    }

    public override void Reset()
    {
        _plugin.Reset();
    }

    public override int LatencyFrames => _plugin.LatencyFrames;

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo()
    {
        var list = new List<AudioParameterInfo>();
        foreach (var p in _plugin.GetParameters())
        {
            if (p.IsHidden || p.IsReadOnly) continue;
            list.Add(new AudioParameterInfo(p.Name, (float)p.MinValue, (float)p.MaxValue,
                (float)p.DefaultValue, "", false));
        }
        return list;
    }

    private void IndexParameters()
    {
        _paramIds.Clear();
        foreach (var p in _plugin.GetParameters())
            _paramIds[p.Name] = p.Id;
    }

    public override bool TryGetParameter(string name, out float value)
    {
        value = 0f;
        if (!_paramIds.TryGetValue(name, out uint id)) return false;
        if (!_plugin.TryGetParamValue(id, out double v)) return false;
        value = (float)v;
        return true;
    }

    public override bool TrySetParameter(string name, float value)
    {
        if (!_paramIds.TryGetValue(name, out uint id)) return false;
        _plugin.SetParamValue(id, value);
        _plugin.FlushParams();
        return true;
    }

    /// <summary>Sets a CLAP parameter directly by clap_id.</summary>
    public bool TrySetParamById(uint paramId, double value)
    {
        _plugin.SetParamValue(paramId, value);
        _plugin.FlushParams();
        return true;
    }

    public override JsonObject GetState()
    {
        var o = new JsonObject { ["plugin"] = _plugin.Id, ["vendor"] = _plugin.Vendor };
        var blob = _plugin.SaveState();
        if (blob is not null) o["state"] = Convert.ToBase64String(blob);
        var ps = new JsonObject();
        foreach (var (name, id) in _paramIds)
            if (_plugin.TryGetParamValue(id, out double v)) ps[name] = v;
        o["params"] = ps;
        return o;
    }

    public override void SetState(JsonObject state)
    {
        if (state["state"]?.GetValue<string>() is { } b64)
        {
            try { _plugin.LoadState(Convert.FromBase64String(b64)); } catch { }
        }
        if (state["params"] is JsonObject ps)
            foreach (var kv in ps)
                if (_paramIds.TryGetValue(kv.Key, out uint id))
                    _plugin.SetParamValue(id, kv.Value?.GetValue<double>() ?? 0);
        _plugin.FlushParams();
    }

    public override void Process(Span<float> buffer)
    {
        int frames = buffer.Length / Format.Channels;
        EnsureScratch(buffer.Length);
        buffer.CopyTo(_inScratch.AsSpan(0, buffer.Length));
        _plugin.FlushParams();
        _plugin.ProcessBlock(_inScratch, _outScratch, frames);
        _outScratch.AsSpan(0, buffer.Length).CopyTo(buffer);
    }

    private void EnsureScratch(int floats)
    {
        if (_inScratch.Length < floats)
        {
            int n = Math.Max(floats, 8192 * Format.Channels);
            _inScratch = new float[n];
            _outScratch = new float[n];
        }
    }

    public void Dispose()
    {
        if (_ownsPlugin) _plugin.Dispose();
    }
}
