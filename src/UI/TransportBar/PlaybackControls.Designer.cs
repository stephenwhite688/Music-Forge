namespace MusicForge.UI.TransportBar;

partial class PlaybackControls
{
    private System.ComponentModel.IContainer components = null;
    private PreviousButtonControl previousButtonControl;
    private PlayButtonControl playButtonControl;
    private StopButtonControl stopButtonControl;
    private NextButtonControl nextButtonControl;
    private RepeatButtonControl repeatButtonControl;
    private ShuffleButtonControl shuffleButtonControl;

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
        previousButtonControl = new PreviousButtonControl();
        playButtonControl = new PlayButtonControl();
        stopButtonControl = new StopButtonControl();
        nextButtonControl = new NextButtonControl();
        repeatButtonControl = new RepeatButtonControl();
        shuffleButtonControl = new ShuffleButtonControl();

        this.SuspendLayout();

        previousButtonControl.Location = new System.Drawing.Point(0, 9);
        playButtonControl.Location = new System.Drawing.Point(32, 9);
        stopButtonControl.Location = new System.Drawing.Point(64, 9);
        nextButtonControl.Location = new System.Drawing.Point(96, 9);
        repeatButtonControl.Location = new System.Drawing.Point(128, 9);
        shuffleButtonControl.Location = new System.Drawing.Point(160, 9);

        Controls.Add(previousButtonControl);
        Controls.Add(playButtonControl);
        Controls.Add(stopButtonControl);
        Controls.Add(nextButtonControl);
        Controls.Add(repeatButtonControl);
        Controls.Add(shuffleButtonControl);

        this.Name = "PlaybackControls";
        this.Size = new System.Drawing.Size(192, 50);
        this.ResumeLayout(false);
    }
}
