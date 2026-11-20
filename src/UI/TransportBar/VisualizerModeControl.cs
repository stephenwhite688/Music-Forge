using System;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class VisualizerModeControl : UserControl
{
    public event EventHandler? ModeChanged;

    private bool barsMode = true;

    public bool BarsMode => barsMode;

    public VisualizerModeControl()
    {
        InitializeComponent();
    }

    private void btnMode_Click(object sender, EventArgs e)
    {
        barsMode = !barsMode;

        btnMode.Text = barsMode
            ? "Bars"
            : "Scope";

        ModeChanged?.Invoke(this, EventArgs.Empty);
    }
}
