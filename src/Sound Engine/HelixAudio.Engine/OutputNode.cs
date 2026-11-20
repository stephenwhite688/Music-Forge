namespace HelixAudio.Engine;
using HelixAudio.Core;
using System;
public class OutputNode : EngineNode
{
    private readonly IAudioDeviceManager _dm;
    public OutputNode(int sr, int ch, IAudioDeviceManager dm, string name = "Output")
        : base(sr, ch, NodeType.Output, name) { _dm = dm ?? throw new ArgumentNullException(nameof(dm)); }
    public override float[] Process(float[] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (input.Length == 0)
            throw new ArgumentException("Buffer cannot be empty.", nameof(input));
        if (input.Length % Channels != 0)
            throw new ArgumentException($"Buffer length must be a multiple of channel count ({Channels}).", nameof(input));

        _dm.Write(input);
        return input;
    }
}
