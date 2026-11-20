using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class RepeatButtonControl : UserControl
{
    public event EventHandler? RepeatClicked;

    public RepeatButtonControl()
    {
        InitializeComponent();

        string imagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "Transport",
            "TransportBar",
            "repeat_none.png");

        if (File.Exists(imagePath))
        {
            pictureBox.Image = Image.FromFile(imagePath);
        }

        pictureBox.Click += PictureBox_Click;
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        RepeatClicked?.Invoke(this, EventArgs.Empty);
    }
}
