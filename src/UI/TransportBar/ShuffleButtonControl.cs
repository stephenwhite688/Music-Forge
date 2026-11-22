using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MusicForge.UI.TransportBar;

/// <summary>Shuffle toggle button with an active-state highlight.</summary>
public partial class ShuffleButtonControl : UserControl
{
    public event EventHandler? ShuffleClicked;

    private bool _active;

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

    /// <summary>Highlights the button while shuffle is enabled.</summary>
    public void SetActive(bool active)
    {
        _active = active;
        BackColor = active ? Color.FromArgb(0, 122, 204) : Color.Transparent;
    }

    private void PictureBox_Click(object? sender, EventArgs e)
    {
        ShuffleClicked?.Invoke(this, EventArgs.Empty);
    }
}
