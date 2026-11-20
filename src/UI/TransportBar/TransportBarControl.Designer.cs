namespace AudioForge.UI.TransportBar;

partial class TransportBarControl
{
    private PlaybackControls playbackControls;
    private TrackInfoControl trackInfoControl;
    private CurrentTimeControl currentTimeControl;
    private TotalTimeControl totalTimeControl;
    private VolumeControl volumeControl;
    private VisualizerPowerControl visualizerPowerControl;
    private VisualizerModeControl visualizerModeControl;
    private MiniVisualizerControl miniVisualizerControl;

    private void InitializeComponent()
    {
        SuspendLayout();

        playbackControls = new PlaybackControls();
        trackInfoControl = new TrackInfoControl();
        currentTimeControl = new CurrentTimeControl();
        totalTimeControl = new TotalTimeControl();
        volumeControl = new VolumeControl();
        visualizerPowerControl = new VisualizerPowerControl();
        visualizerModeControl = new VisualizerModeControl();
        miniVisualizerControl = new MiniVisualizerControl();

        playbackControls.Location = new System.Drawing.Point(5,5);

        trackInfoControl.Location = new System.Drawing.Point(310,5);

        currentTimeControl.Location = new System.Drawing.Point(570,5);

        totalTimeControl.Location = new System.Drawing.Point(655,5);

        volumeControl.Location = new System.Drawing.Point(740,0);

        visualizerPowerControl.Location = new System.Drawing.Point(930,5);

        visualizerModeControl.Location = new System.Drawing.Point(995,5);

        miniVisualizerControl.Location = new System.Drawing.Point(1075,5);

        Controls.Add(playbackControls);
        Controls.Add(trackInfoControl);
        Controls.Add(currentTimeControl);
        Controls.Add(totalTimeControl);
        Controls.Add(volumeControl);
        Controls.Add(visualizerPowerControl);
        Controls.Add(visualizerModeControl);
        Controls.Add(miniVisualizerControl);

        Name = "TransportBarControl";
        Size = new System.Drawing.Size(1260,50);

        ResumeLayout(false);
    }
}
