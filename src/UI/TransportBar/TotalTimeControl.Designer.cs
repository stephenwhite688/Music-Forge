namespace AudioForge.UI.TransportBar;

partial class TotalTimeControl
{
    private Label lblTotalTime;

    private void InitializeComponent()
    {
        lblTotalTime = new Label();

        SuspendLayout();

        lblTotalTime.Dock = DockStyle.Fill;
        lblTotalTime.Text = "00:00:00";
        lblTotalTime.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

        Controls.Add(lblTotalTime);

        Size = new System.Drawing.Size(80,32);

        ResumeLayout(false);
    }
}
