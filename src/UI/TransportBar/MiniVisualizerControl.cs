using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class MiniVisualizerControl : UserControl
{
    private readonly System.Windows.Forms.Timer refreshTimer;
    private readonly BarVisualizerRenderer barRenderer;
    private readonly OscilloscopeRenderer oscilloscopeRenderer;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseBars { get; set; } = true;

    public MiniVisualizerControl()
    {
        InitializeComponent();

        barRenderer = new BarVisualizerRenderer();
        oscilloscopeRenderer = new OscilloscopeRenderer();

        refreshTimer = new System.Windows.Forms.Timer();
        refreshTimer.Interval = 100;
        refreshTimer.Tick += RefreshTimer_Tick;
        refreshTimer.Start();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var meters = AudioForge.UI.Services.MeterService.Instance;
        if (UseBars)
        {
            barRenderer.Draw(e.Graphics, ClientRectangle, meters.PostEq.SpectrumDb, 24);
        }
        else
        {
            oscilloscopeRenderer.Draw(e.Graphics, ClientRectangle,
                meters.Waveform.SnapshotChannel(0, ClientRectangle.Width));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
        }

        base.Dispose(disposing);
    }
}