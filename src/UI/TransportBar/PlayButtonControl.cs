using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class PlayButtonControl : UserControl
{
    public event EventHandler? PlayClicked;

    private readonly Image? _playImage;
    private readonly Image? _pauseImage;
    private bool _showingPause;

    public PlayButtonControl()
    {
        InitializeComponent();

        string dir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets", "Transport", "TransportBar");

        _playImage = LoadImage(Path.Combine(dir, "Play.png"));
        _pauseImage = LoadImage(Path.Combine(dir, "pause.png"));
        pictureBox.Image = _playImage;

        pictureBox.Click += PictureBox_Click;
    }

    /// <summary>Swaps the icon between play and pause. No-op when unchanged.</summary>
    public void SetPausedState(bool paused)
    {
        if (paused == _showingPause) return;
        _showingPause = paused;
        pictureBox.Image = paused ? _pauseImage : _playImage;
    }

    private static Image? LoadImage(string path) =>
        File.Exists(path) ? Image.FromFile(path) : null;

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        PlayClicked?.Invoke(this, EventArgs.Empty);
    }
}
