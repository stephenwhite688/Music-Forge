#nullable enable

namespace AudioForge.UI.TransportBar;

partial class PreviousButtonControl
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
        ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
        SuspendLayout();
        // 
        // pictureBox
        // 
        pictureBox.BackColor = Color.Transparent;
        pictureBox.Cursor = Cursors.Hand;
        pictureBox.Dock = DockStyle.Fill;
        pictureBox.Location = new Point(0, 0);
        pictureBox.Name = "pictureBox";
        pictureBox.Size = new Size(32, 32);
        pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
        pictureBox.TabIndex = 0;
        pictureBox.TabStop = false;
        pictureBox.Click += PictureBox_Click;
        // 
        // PreviousButtonControl
        // 
        Controls.Add(pictureBox);
        Name = "PreviousButtonControl";
        Size = new Size(32, 32);
        ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
        ResumeLayout(false);
    }
}
