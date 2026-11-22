using System;
using System.Windows.Forms;

namespace MusicForge.UI.TransportBar;

public partial class VisualizerPowerControl : UserControl
{
    public event EventHandler? PowerChanged;

    private bool isOn = true;

    public bool IsOn => isOn;

    public VisualizerPowerControl()
    {
        InitializeComponent();
    }

    private void btnPower_Click(object sender, EventArgs e)
    {
        isOn = !isOn;

        btnPower.Text = isOn
            ? "On"
            : "Off";

        PowerChanged?.Invoke(this, EventArgs.Empty);
    }
}
