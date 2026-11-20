using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class PlayButtonControl : UserControl
{
    public event EventHandler? PlayClicked;

    public PlayButtonControl()
    {
        InitializeComponent();

        string imagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "Transport",
            "TransportBar",
            "Play.png");

        if (File.Exists(imagePath))
        {
            pictureBox.Image = Image.FromFile(imagePath);
        }

        pictureBox.Click += PictureBox_Click;
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        PlayClicked?.Invoke(this, EventArgs.Empty);
    }
}
