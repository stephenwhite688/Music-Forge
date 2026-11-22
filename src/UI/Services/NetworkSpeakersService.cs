using SpeakerForge;
using System;

namespace MusicForge.UI.Services;

/// <summary>
/// UI-facing singleton over the standalone SpeakerForge engine.
/// Owns device discovery, groups, the embedded PCM server, and the broadcast
/// node that taps the playback chain.
/// </summary>
public sealed class NetworkSpeakersService
{
    public static NetworkSpeakersService Instance { get; } = new NetworkSpeakersService();

    public NetworkSpeakerService Engine { get; } = new();

    /// <summary>Chain node that feeds processed audio to connected renderers.</summary>
    public PcmBroadcastNode BroadcastNode { get; }

    /// <summary>Raised when the device list or connection count changes (UI thread marshal needed).</summary>
    public event EventHandler? Changed;

    private NetworkSpeakersService()
    {
        BroadcastNode = new PcmBroadcastNode(Engine.Server);
    }

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Starts the embedded audio server (idempotent).</summary>
    public void EnsureServerRunning()
    {
        try { Engine.Start(); } catch { }
    }
}
