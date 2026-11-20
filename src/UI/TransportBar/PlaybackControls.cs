using System;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class PlaybackControls : UserControl
{
    public event EventHandler? PlayClicked;
    public event EventHandler? PauseClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? NextClicked;
    public event EventHandler? PreviousClicked;
    public event EventHandler? RepeatClicked;
    public event EventHandler? ShuffleClicked;

    public PlaybackControls()
    {
        InitializeComponent();

        playButtonControl.PlayClicked += (s, e) => PlayClicked?.Invoke(this, e);
        pauseButtonControl.PauseClicked += (s, e) => PauseClicked?.Invoke(this, e);
        stopButtonControl.StopClicked += (s, e) => StopClicked?.Invoke(this, e);
        nextButtonControl.NextClicked += (s, e) => NextClicked?.Invoke(this, e);
        previousButtonControl.PreviousClicked += (s, e) => PreviousClicked?.Invoke(this, e);
        repeatButtonControl.RepeatClicked += (s, e) => RepeatClicked?.Invoke(this, e);
        shuffleButtonControl.ShuffleClicked += (s, e) => ShuffleClicked?.Invoke(this, e);
    }
}
