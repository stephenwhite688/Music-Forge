#nullable enable

namespace AudioForge.UI.TransportBar;

partial class ShuffleButtonControl
{
    private System.ComponentModel.IContainer? components = null;
    private PictureBox pictureBox;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pictureBox = new PictureBox();

        SuspendLayout();

        pictureBox.Dock = DockStyle.Fill;
        pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
        pictureBox.Cursor = Cursors.Hand;
        pictureBox.BackColor = Color.Transparent;

        Controls.Add(pictureBox);

        Name = "ShuffleButtonControl";
        Size = new System.Drawing.Size(32, 32);

        ResumeLayout(false);
    }
}
