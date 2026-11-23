namespace AudioForge.PluginHost;
using System.Collections.Generic;
public interface IAudioPlugin
{
    string Name { get; }
    string Author { get; }
    string Version { get; }
    PluginType Type { get; }
    void Initialize(int sampleRate, int channels);
    void Reset();
    float[] Process(float[] input);
    Dictionary<string, float> GetParameters();
    void SetParameter(string name, float value);
}
