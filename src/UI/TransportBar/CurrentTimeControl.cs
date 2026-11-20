using System.ComponentModel;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

public partial class CurrentTimeControl : UserControl
{
    public CurrentTimeControl()
    {
        InitializeComponent();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CurrentTime
    {
        get => lblCurrentTime.Text;
        set => lblCurrentTime.Text = value;
    }
}
