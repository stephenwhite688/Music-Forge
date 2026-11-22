using AudioForge.UI.Data;
using HelixAudio.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AudioForge.UI.Services;

/// <summary>
/// UI-facing playback service: owns the engine's <see cref="RealtimePlayer"/>,
/// the current playlist queue, transport commands, and position polling.
/// Singleton - access via <see cref="Instance"/>.
/// </summary>
public sealed class PlaybackService : IDisposable
{
    public static PlaybackService Instance { get; } = new PlaybackService();

    private readonly RealtimePlayer _player = new();
    private readonly System.Windows.Forms.Timer _positionTimer;
    private readonly SynchronizationContext? _uiContext;
    private List<string> _queue = new();
    private int _index = -1;
    private string? _stationName;
    private string? _stationUrl;
    private bool _stationUnavailable; // looping the "station not available" clip
    private bool _disposed;

    /// <summary>Raised on the UI thread when the current track changes.</summary>
    public event EventHandler? TrackChanged;

    /// <summary>Raised on the UI thread ~4x/sec while playing (position updates).</summary>
    public event EventHandler? PositionChanged;

    /// <summary>Raised when playback completes or stops.</summary>
    public event EventHandler? PlaybackStopped;

    public bool IsPlaying => _player.IsPlaying;
    public bool IsPaused => _player.IsPaused;
    public string? CurrentTrackPath => _index >= 0 && _index < _queue.Count ? _queue[_index] : null;
    public string CurrentTrackName => _stationName ?? (CurrentTrackPath is { } p ? Path.GetFileNameWithoutExtension(p) : "");
    /// <summary>Stream URL of the currently playing radio station, if any.</summary>
    public string? CurrentStationUrl => _stationUrl;
    /// <summary>True while a radio station (not a file) is loaded.</summary>
    public bool IsStationPlaying => _stationName is not null;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.Duration;
    public int QueueCount => _queue.Count;

    private PlaybackService()
    {
        _uiContext = SynchronizationContext.Current; // capture UI thread
        _player.PlaybackCompleted += OnPlaybackCompleted;
        _player.PlaybackError += OnPlaybackError;

        _positionTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _positionTimer.Tick += (s, e) =>
        {
            if (_player.IsPlaying || _player.IsPaused) PositionChanged?.Invoke(this, EventArgs.Empty);
        };
        _positionTimer.Start();
    }

    /// <summary>Loads a playlist from the database and starts playing it.</summary>
    /// <param name="startIndex">Track position to start from (0 = first).</param>
    public async Task PlayPlaylistAsync(int playlistId, int startIndex = 0)
    {
        var paths = new LibraryRepository().GetPlaylistTrackPaths(playlistId);
        if (paths.Count == 0)
        {
            MessageBox.Show("This playlist has no tracks.", "Playback",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        await PlayQueueAsync(paths, startIndex);
    }

    /// <summary>Plays an explicit queue of file paths.</summary>
    /// <param name="startIndex">Track position to start from (0 = first).</param>
    public async Task PlayQueueAsync(IReadOnlyList<string> paths, int startIndex = 0)
    {
        StopInternal();
        _stationName = null;
        _queue = new List<string>(paths);
        _index = Math.Clamp(startIndex, 0, _queue.Count - 1);
        Log($"Queue loaded: {paths.Count} tracks, startIndex={startIndex} -> {_index}");
        for (int i = 0; i < paths.Count; i++)
            Log($"  [{i}] {paths[i]}");
        await PlayCurrentAsync();
    }

    /// <summary>Plays a live internet radio stream through the same engine/output as files.</summary>
    /// <param name="stationId">Cached Stations table Id (0 = not in directory).</param>
    public void PlayStation(string stationName, string streamUrl, int stationId = 0)
    {
        _stationName = stationName;
        _stationUrl = streamUrl;
        _stationUnavailable = false;
        _queue.Clear();
        _index = -1;
        try
        {
            _player.PlayStream(streamUrl, nodes: MeterService.Instance.Chain());
            if (stationId > 0)
                new RadioRepository().RecordPlay(stationId, stationName, streamUrl);
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _stationName = null;
            _stationUrl = null;
            Log($"STATION PLAY FAILED {stationName} {streamUrl} err={ex.Message}");
            // The spoken clip IS the error message - loop it until another
            // station is chosen or playback is stopped.
            _ = PlayUnavailableLoopAsync();
        }
    }

    /// <summary>
    /// Plays a media URL through the engine: HLS (.m3u8) via
    /// <see cref="HelixAudio.Streaming.HlsStream"/>, DASH (.mpd) via
    /// <see cref="HelixAudio.Streaming.DashStream"/>, anything else via the
    /// Media Foundation live-stream path. All go through the same node chain
    /// and output device as file playback.
    /// </summary>
    public async Task PlayUrlAsync(string url)
    {
        _stationName = System.IO.Path.GetFileName(new Uri(url).AbsolutePath) is { Length: > 0 } n ? n : url;
        if (_stationName.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
            _stationName.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
            _stationName = new Uri(url).Host;
        _stationUrl = url;
        _stationUnavailable = false;
        _queue.Clear();
        _index = -1;
        try
        {
            var path = new Uri(url).AbsolutePath;
            HelixAudio.IO.AudioStream stream;
            if (path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
                stream = await HelixAudio.Streaming.HlsStream.OpenAsync(url);
            else if (path.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
                stream = await HelixAudio.Streaming.DashStream.OpenAsync(url);
            else
            {
                _player.PlayStream(url, nodes: MeterService.Instance.Chain());
                if (!string.IsNullOrEmpty(_stationName))
                    TrackChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
            _player.PlayAudioStream(stream, MeterService.Instance.Chain());
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _stationName = null;
            _stationUrl = null;
            Log($"STREAM PLAY FAILED {url} err={ex.Message}");
            MessageBox.Show($"Could not play stream:\n{url}\n\n{ex.Message}",
                "Playback", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Path to the spoken "station not available" clip shipped in Assets.</summary>
    private static string UnavailableClipPath => Path.Combine(
        AppContext.BaseDirectory, "Assets", "InternetRadio", "station is not available.mp3");

    /// <summary>Loops the unavailable-station clip through the engine until stopped.</summary>
    private async Task PlayUnavailableLoopAsync()
    {
        _stationUnavailable = true;
        _stationName = "Station not available";
        _stationUrl = null;
        _queue.Clear();
        _index = -1;
        try
        {
            await _player.PlayAsync(UnavailableClipPath, MeterService.Instance.Chain());
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log($"UNAVAILABLE CLIP FAILED {ex.Message}");
            _stationUnavailable = false;
            _stationName = null;
        }
    }

    /// <summary>Play/Pause toggle matching the transport buttons.</summary>
    public void TogglePlayPause()
    {
        if (_player.IsPaused) _player.Resume();
        else if (_player.IsPlaying) _player.Pause();
    }

    /// <summary>Pauses playback if playing; does nothing when already paused/stopped.</summary>
    public void Pause() => _player.Pause();

    public void Next()
    {
        if (_queue.Count == 0) return;
        _index = (_index + 1) % _queue.Count;
        _ = PlayCurrentAsync();
    }

    public void Previous()
    {
        if (_queue.Count == 0) return;
        _index = _index <= 0 ? _queue.Count - 1 : _index - 1;
        _ = PlayCurrentAsync();
    }

    public void Stop()
    {
        StopInternal();
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Current output volume 0..1.</summary>
    public float Volume => _player.Volume;

    public void SetVolume(float volume01) => _player.Volume = volume01;

    /// <summary>All active Windows render endpoints (speakers, Bluetooth, USB DACs, HDMI...).</summary>
    public IReadOnlyList<(string Id, string Name)> OutputDevices =>
        HelixAudio.Backend.Wasapi.WasapiDeviceManager.EnumerateOutputDevices();

    /// <summary>Selected output endpoint ID; null/empty = Windows default. Applies to next play.</summary>
    public string? OutputDeviceId
    {
        get => string.IsNullOrEmpty(_player.OutputDeviceId) ? null : _player.OutputDeviceId;
        set => _player.OutputDeviceId = value ?? "";
    }

    /// <summary>
    /// Plays a 3-second 440 Hz test tone through the full processing chain
    /// (EQ, mixer, meters, taps) without touching the queue. Used to verify
    /// the visualiser and engine taps.
    /// </summary>
    public async Task PlayTestToneAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "audioforge-test-tone.wav");
        WriteTestToneWav(path);
        await _player.PlayAsync(path, MeterService.Instance.Chain());
    }

    private static void WriteTestToneWav(string path)
    {
        const int sampleRate = 48000, channels = 2, seconds = 3;
        int frames = sampleRate * seconds;
        int dataSize = frames * channels * 2;
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16); w.Write((short)1); w.Write((short)channels);
        w.Write(sampleRate); w.Write(sampleRate * channels * 2);
        w.Write((short)(channels * 2)); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);
        for (int i = 0; i < frames; i++)
        {
            double t = (double)i / sampleRate;
            // 5 ms fade in/out to avoid clicks
            float env = (float)Math.Min(1.0, Math.Min(t / 0.005, (seconds - t) / 0.005));
            short s = (short)(Math.Sin(2 * Math.PI * 440 * t) * 0.3 * env * 32767);
            w.Write(s); w.Write(s);
        }
    }

    private async Task PlayCurrentAsync()
    {
        if (_index < 0 || _index >= _queue.Count) return;
        Log($"Play index={_index}/{_queue.Count} path={_queue[_index]}");
        if (HelixAudio.IO.Codecs.CodecManager.GetTrustForFile(_queue[_index]) is { } trust)
            Log($"  DECODE codec={trust.CodecName} backend=\"{trust.Backend}\" verified={(trust.VerifiedDecoder ? "yes" : "NO - system decoder")} ");
        try
        {
            await _player.PlayAsync(_queue[_index], MeterService.Instance.Chain());
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log($"PLAY FAILED index={_index} path={_queue[_index]} err={ex.Message}");
            MessageBox.Show($"Could not play:\n{_queue[_index]}\n\n{ex.Message}",
                "Playback", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Next();
        }
    }

    private static void Log(string message)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "audioforge-playback.log");
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
        }
        catch { }
    }

    /// <summary>Marshals an action onto the UI thread (engine events fire on background threads).</summary>
    private void OnUi(Action action)
    {
        if (_uiContext is not null) _uiContext.Post(_ => action(), null);
        else action();
    }

    private void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        // Engine fires this on a background thread - marshal to UI
        OnUi(() =>
        {
            // Unavailable clip finished - loop it until stopped/another station
            if (_stationUnavailable)
            {
                _ = PlayUnavailableLoopAsync();
                return;
            }
            // Radio stream ended = station dropped - switch to the spoken clip
            if (_stationName is not null && _queue.Count == 0)
            {
                Log($"STATION DROPPED {_stationName}");
                _ = PlayUnavailableLoopAsync();
                return;
            }
            // Auto-advance to next track; stop at end of queue
            if (_index >= 0 && _index < _queue.Count - 1)
            {
                _index++;
                _ = PlayCurrentAsync();
            }
            else
            {
                _index = -1;
                TrackChanged?.Invoke(this, EventArgs.Empty);
                PlaybackStopped?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void OnPlaybackError(object? sender, Exception ex)
    {
        OnUi(() =>
        {
            Log($"PLAYBACK ERROR {ex.Message}");
            // Station failures speak through the loop clip instead of a dialog
            if (_stationName is not null || _stationUnavailable)
            {
                _ = PlayUnavailableLoopAsync();
                return;
            }
            MessageBox.Show($"Playback error:\n{ex.Message}", "Playback",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Stop();
        });
    }

    private void StopInternal()
    {
        _player.Stop();
        _queue.Clear();
        _index = -1;
        _stationName = null;
        _stationUrl = null;
        _stationUnavailable = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _positionTimer.Dispose();
        _player.Dispose();
    }
}
