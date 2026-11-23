namespace AudioForge.PluginHost;
using System.Collections.Generic;
public class BuiltinPlugin : IAudioPlugin
{
    private Dictionary<string, float> _params = new Dictionary<string, float>();
    public string Name => "BuiltinPlugin";
    public string Author => "AudioForge";
    public string Version => "1.0.0";
    public PluginType Type => PluginType.Custom;
    public void Initialize(int sr, int ch) { }
    public void Reset() { _params.Clear(); }
    public float[] Process(float[] input) { return input; }
    public Dictionary<string, float> GetParameters() => new Dictionary<string, float>(_params);
    public void SetParameter(string name, float value) { _params[name] = value; }
}
