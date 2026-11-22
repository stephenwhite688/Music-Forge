using MusicForge.UI.Mixer;
using System.Windows.Forms;

namespace MusicForge.UI.MainWindow.Ribbons;

public partial class MixerRibbonControl : RibbonStripControl
{
    private readonly MixerPanelControl _mixerPanel = new();
    private readonly Panel _tabHost = new() { Dock = DockStyle.Fill };

    public override Control? TabContent => _tabHost;

    public MixerRibbonControl()
    {
        InitializeComponent();
        _tabHost.Controls.Add(_mixerPanel);
    }
}
