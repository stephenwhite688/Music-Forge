using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class PreviousButtonControl : UserControl
{
    public event EventHandler? PreviousClicked;

    public PreviousButtonControl()
    {
        InitializeComponent();

        string imagePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets",
            "Transport",
            "TransportBar",
            "prev.png");

        if (File.Exists(imagePath))
        {
            pictureBox.Image = Image.FromFile(imagePath);
        }

        pictureBox.Click += PictureBox_Click;
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        PreviousClicked?.Invoke(this, EventArgs.Empty);
    }

    private void pictureBox_Click_1(object sender, EventArgs e)
    {

    }
}
