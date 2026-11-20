namespace AudioForge.UI.TransportBar;

partial class CurrentTimeControl
{
    private Label lblCurrentTime;

    private void InitializeComponent()
    {
        lblCurrentTime = new Label();

        SuspendLayout();

        lblCurrentTime.Dock = DockStyle.Fill;
        lblCurrentTime.Text = "00:00:00";
        lblCurrentTime.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

        Controls.Add(lblCurrentTime);

        Size = new System.Drawing.Size(80,32);

        ResumeLayout(false);
    }
}
