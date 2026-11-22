namespace MusicForge.UI.TransportBar;

partial class TrackInfoControl
{
    private Label lblTrackName;

    private void InitializeComponent()
    {
        lblTrackName = new Label();

        SuspendLayout();

        lblTrackName.Dock = DockStyle.Fill;
        lblTrackName.Text = "No Track Loaded";
        lblTrackName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        lblTrackName.Padding = new Padding(5,0,0,0);

        Controls.Add(lblTrackName);

        Size = new System.Drawing.Size(250,32);

        ResumeLayout(false);
    }
}
