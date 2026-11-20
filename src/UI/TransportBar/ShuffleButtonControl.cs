using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class ShuffleButtonControl : UserControl
{
    public event EventHandler? ShuffleClicked;

    public ShuffleButtonControl()
    {
        InitializeComponent();

        string imagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "Transport",
            "TransportBar",
            "random.png");

        if (File.Exists(imagePath))
        {
            pictureBox.Image = Image.FromFile(imagePath);
        }

        pictureBox.Click += PictureBox_Click;
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        ShuffleClicked?.Invoke(this, EventArgs.Empty);
    }
}
