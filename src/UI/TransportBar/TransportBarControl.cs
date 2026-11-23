using System;
using System.Drawing;
using System.Windows.Forms;
using MusicForge.UI.Services;
using MusicForge.UI.Shared;

namespace MusicForge.UI.TransportBar;

public partial class TransportBarControl : UserControl
{
    private bool _busy;

    /// <summary>The mini visualiser - used by Options low-power mode to hide decorative rendering.</summary>
    public MiniVisualizerControl MiniVisualizer => miniVisualizerControl;

    public TransportBarControl()
    {
        InitializeComponent();

        visualizerModeControl.ModeChanged += VisualizerModeControl_ModeChanged;
        visualizerPowerControl.PowerChanged += VisualizerPowerControl_PowerChanged;

        var playback = PlaybackService.Instance;
        playbackControls.PlayClicked += (s, e) => playback.TogglePlayPause();
        playbackControls.StopClicked += (s, e) => playback.Stop();
        playbackControls.NextClicked += (s, e) => playback.Next();
        playbackControls.PreviousClicked += (s, e) => playback.Previous();
        playbackControls.ShuffleClicked += (s, e) =>
        {
            playbackControls.SetShuffleActive(playback.ToggleShuffle());
        };
        playbackControls.RepeatClicked += (s, e) =>
        {
            var mode = playback.CycleRepeat();
            playbackControls.SetRepeatMode(mode switch
            {
                PlaybackService.RepeatMode.All => 1,
                PlaybackService.RepeatMode.One => 2,
                _ => 0
            });
        };
        playbackControls.SetShuffleActive(playback.ShuffleEnabled);
        playbackControls.SetRepeatMode(playback.Repeat switch
        {
            PlaybackService.RepeatMode.All => 1,
            PlaybackService.RepeatMode.One => 2,
            _ => 0
        });
        volumeControl.VolumeChanged += (s, v) => playback.SetVolume(v / 100f);
        playback.SetVolume(volumeControl.Volume / 100f);

        playback.TrackChanged += Playback_TrackChanged;
        playback.PositionChanged += Playback_PositionChanged;
        playback.PlaybackStopped += Playback_Stopped;

        ThemeManager.ThemeChanged += ApplyTheme;
        ApplyTheme();
    }

    private void Playback_TrackChanged(object? sender, EventArgs e)
    {
        var playback = PlaybackService.Instance;
        trackInfoControl.TrackName = playback.CurrentTrackName;
        totalTimeControl.TotalTime = FormatTime(playback.Duration);
        currentTimeControl.CurrentTime = "00:00";
    }

    private void Playback_PositionChanged(object? sender, EventArgs e)
    {
        if (_busy) return; // don't overwrite progress display during long operations
        var playback = PlaybackService.Instance;
        currentTimeControl.CurrentTime = FormatTime(playback.Position);
        playbackControls.SetPlayButtonPaused(playback.IsPaused);
    }

    /// <summary>Shows a long-running operation (e.g. directory load) in the transport bar.</summary>
    public void ShowOperationProgress(string title)
    {
        _busy = true;
        trackInfoControl.TrackName = title;
        currentTimeControl.CurrentTime = "00:00";
        totalTimeControl.TotalTime = "--:--";
    }

    /// <summary>Updates elapsed and estimated-total time while <see cref="ShowOperationProgress"/> is active.</summary>
    public void UpdateOperationProgress(TimeSpan elapsed, TimeSpan estimatedTotal)
    {
        if (!_busy) return;
        currentTimeControl.CurrentTime = FormatTime(elapsed);
        totalTimeControl.TotalTime = FormatTime(estimatedTotal);
    }

    /// <summary>Ends the operation display and restores normal transport readout.</summary>
    public void EndOperationProgress()
    {
        _busy = false;
        var playback = PlaybackService.Instance;
        trackInfoControl.TrackName = playback.CurrentTrackName;
        totalTimeControl.TotalTime = FormatTime(playback.Duration);
        currentTimeControl.CurrentTime = playback.IsPlaying || playback.IsPaused
            ? FormatTime(playback.Position) : "00:00";
    }

    private void Playback_Stopped(object? sender, EventArgs e)
    {
        currentTimeControl.CurrentTime = "00:00";
        trackInfoControl.TrackName = "";
        totalTimeControl.TotalTime = "00:00";
        playbackControls.SetPlayButtonPaused(false);
    }

    private static string FormatTime(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes:D2}:{t.Seconds:D2}";
    }

    private void VisualizerPowerControl_PowerChanged(object? sender, System.EventArgs e)
    {
        miniVisualizerControl.Visible =
            visualizerPowerControl.IsOn;
    }

    private void VisualizerModeControl_ModeChanged(object? sender, System.EventArgs e)
    {
        miniVisualizerControl.UseBars =
            visualizerModeControl.BarsMode;
    }

    private void ApplyTheme()
    {
        ThemeModel theme = ThemeManager.Current;
        BackColor = theme.ControlDefaultSurface;
        ApplyThemeToChildren(this, theme);
    }

    private static void ApplyThemeToChildren(Control parent, ThemeModel theme)
    {
        foreach (Control child in parent.Controls)
        {
            bool isVisualizerButton = child is Button &&
                child.Parent is VisualizerModeControl or VisualizerPowerControl;

            child.BackColor = child switch
            {
                MiniVisualizerControl => theme.OscilloscopeBackground,
                _ when isVisualizerButton => Color.FromArgb(190, 190, 190),
                Button => theme.PrimaryAccent,
                TrackBar => theme.SliderTrackBackground,
                _ => theme.ControlDefaultSurface
            };

            child.ForeColor = isVisualizerButton
                ? Color.FromArgb(40, 40, 40)
                : theme.TextPrimary;

            if (child is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = theme.BorderThickness;
            }

            if (child is UserControl userControl)
            {
                userControl.BorderStyle = BorderStyle.None;
            }

            ApplyThemeToChildren(child, theme);
        }
    }
}
