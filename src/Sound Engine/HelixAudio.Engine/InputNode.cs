namespace HelixAudio.Engine;
using HelixAudio.Core;
public class InputNode : EngineNode
{
    private readonly IAudioDeviceManager _dm;
    public InputNode(int sr, int ch, IAudioDeviceManager dm = null, string name = "Input")
        : base(sr, ch, NodeType.Input, name) { _dm = dm; }
    public override float[] Process(float[] input)
    {
        if (_dm != null) return _dm.Read(input?.Length ?? 0);
        return input ?? new float[0];
    }
}
