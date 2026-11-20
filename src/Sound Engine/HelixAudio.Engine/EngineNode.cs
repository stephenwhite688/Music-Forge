namespace HelixAudio.Engine;
using HelixAudio.Core;
using System;
using System.Collections.Generic;

public enum NodeType { Input, Output, Mixer, Effect, Filter, Analyzer, Generator, Custom, Analysis }

public abstract class EngineNode : AudioNode
{
    public NodeType Type { get; }
    public Guid Id { get; }
    public List<EngineNode> Inputs { get; } = new();
    public List<EngineNode> Outputs { get; } = new();
    public bool Enabled { get; set; } = true;

    protected EngineNode(int sr, int ch, NodeType type, string name)
        : base(sr, ch, name ?? $"{type}_{Guid.NewGuid().ToString().Substring(0,8)}") { Type = type; Id = Guid.NewGuid(); }

    public void ConnectTo(EngineNode target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (!Outputs.Contains(target))
        {
            Outputs.Add(target);
            target.Inputs.Add(this);
        }
    }

    public void DisconnectFrom(EngineNode target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        Outputs.Remove(target);
        target.Inputs.Remove(this);
    }

    public void DisconnectAll()
    {
        foreach (var node in Outputs.ToList())
        {
            node.Inputs.Remove(this);
        }
        Outputs.Clear();

        foreach (var node in Inputs.ToList())
        {
            node.Outputs.Remove(this);
        }
        Inputs.Clear();
    }
}
