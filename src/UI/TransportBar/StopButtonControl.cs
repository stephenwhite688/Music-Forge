using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MusicForge.UI.TransportBar;

public partial class StopButtonControl : UserControl
{
    public event EventHandler? StopClicked;

    public StopButtonControl()
    {
        InitializeComponent();

        string imagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "Transport",
            "TransportBar",
            "stop.png");

        if (File.Exists(imagePath))
        {
            pictureBox.Image = Image.FromFile(imagePath);
        }

        pictureBox.Click += PictureBox_Click;
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        StopClicked?.Invoke(this, EventArgs.Empty);
    }
}
