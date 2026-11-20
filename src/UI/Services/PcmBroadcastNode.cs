using System;
using SpeakerForge;
using HelixAudio.Core;

namespace AudioForge.UI.Services;

/// <summary>
/// Transparent <see cref="AudioNode"/> that taps the processed audio stream and
/// broadcasts each block to the <see cref="NetworkAudioServer"/> for network
/// speakers. Insert at the END of the processing chain so speakers receive the
/// fully processed signal (EQ, meters, etc.). Costs one array copy per
/// connected client per block; no-op when no renderers are connected.
/// </summary>
public sealed class PcmBroadcastNode : AudioNode
{
    private readonly NetworkAudioServer _server;
    private int _channels;
    private int _framesPerBlock;

    public PcmBroadcastNode(NetworkAudioServer server, int sampleRate = 48000, int channels = 2, string name = "NetworkBroadcast")
        : base(sampleRate, channels, name)
    {
        _server = server;
    }

    /// <summary>True when at least one renderer is pulling audio.</summary>
    public bool HasListeners => _server.ConnectedClients > 0;

    /// <summary>When false the node passes audio through without broadcasting (local-only output).</summary>
    public bool Enabled { get; set; } = true;

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        _channels = Math.Max(1, format.Channels);
        _server.SourceChannels = _channels;
        _server.SampleRate = format.SampleRate;
    }

    public override float[] Process(float[] buffer)
    {
        if (buffer.Length == 0) return buffer;
        int frames = buffer.Length / Math.Max(1, _channels);
        if (Enabled && frames > 0 && _server.ConnectedClients > 0)
        {
            _server.SampleRate = Format?.SampleRate ?? SampleRate;
            _server.ChunkFrames = frames;
            _server.Broadcast(buffer, frames);
        }
        return buffer;
    }
}
