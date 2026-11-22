using System;
using System.Windows.Forms;

namespace MusicForge.UI.TransportBar;

public partial class PlaybackControls : UserControl
{
    public event EventHandler? PlayClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? NextClicked;
    public event EventHandler? PreviousClicked;
    public event EventHandler? RepeatClicked;
    public event EventHandler? ShuffleClicked;

    public PlaybackControls()
    {
        InitializeComponent();

        playButtonControl.PlayClicked += (s, e) => PlayClicked?.Invoke(this, e);
        stopButtonControl.StopClicked += (s, e) => StopClicked?.Invoke(this, e);
        nextButtonControl.NextClicked += (s, e) => NextClicked?.Invoke(this, e);
        previousButtonControl.PreviousClicked += (s, e) => PreviousClicked?.Invoke(this, e);
        repeatButtonControl.RepeatClicked += (s, e) => RepeatClicked?.Invoke(this, e);
        shuffleButtonControl.ShuffleClicked += (s, e) => ShuffleClicked?.Invoke(this, e);
    }

    /// <summary>Shows the pause icon while playback is paused, play icon otherwise.</summary>
    public void SetPlayButtonPaused(bool paused) => playButtonControl.SetPausedState(paused);

    /// <summary>Repeat visual state: 0=off, 1=all, 2=one.</summary>
    public void SetRepeatMode(int mode) => repeatButtonControl.SetMode(mode);

    /// <summary>Highlights the shuffle button while enabled.</summary>
    public void SetShuffleActive(bool active) => shuffleButtonControl.SetActive(active);
}
