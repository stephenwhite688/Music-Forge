using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class PauseButtonControl : UserControl
{
    public event EventHandler? PauseClicked;

    public PauseButtonControl()
    {
        InitializeComponent();

        string imagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "Transport",
            "TransportBar",
            "pause.png");

        if (File.Exists(imagePath))
        {
            pictureBox.Image = Image.FromFile(imagePath);
        }

        pictureBox.Click += PictureBox_Click;
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        PauseClicked?.Invoke(this, EventArgs.Empty);
    }
}
