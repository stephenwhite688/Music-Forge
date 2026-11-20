using System.ComponentModel;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class TrackInfoControl : UserControl
{
    public TrackInfoControl()
    {
        InitializeComponent();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TrackName
    {
        get => lblTrackName.Text;
        set => lblTrackName.Text = value;
    }
}
