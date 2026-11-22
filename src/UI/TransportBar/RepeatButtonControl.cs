using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MusicForge.UI.TransportBar;

/// <summary>Repeat-mode button: cycles none -> all -> one with matching icons.</summary>
public partial class RepeatButtonControl : UserControl
{
    public event EventHandler? RepeatClicked;

    private static readonly string[] ModeImages = { "repeat_none.png", "repeat_all.png.png", "repeat_one.png" };
    private int _mode; // 0=off 1=all 2=one

    public RepeatButtonControl()
    {
        InitializeComponent();
        SetModeImage();
        pictureBox.Click += PictureBox_Click;
    }

    /// <summary>Sets the visual state: 0=off, 1=all, 2=one.</summary>
    public void SetMode(int mode)
    {
        _mode = Math.Clamp(mode, 0, 2);
        SetModeImage();
    }

    private void SetModeImage()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Assets", "Transport", "TransportBar", ModeImages[_mode]);
        if (!File.Exists(path) && _mode == 1)
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "Transport", "TransportBar", "repeat_all.png");
        if (File.Exists(path))
        {
            var old = pictureBox.Image;
            pictureBox.Image = Image.FromFile(path);
            old?.Dispose();
        }
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        RepeatClicked?.Invoke(this, EventArgs.Empty);
    }
}
