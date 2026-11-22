using System.ComponentModel;
using System.Windows.Forms;

namespace MusicForge.UI.TransportBar;

public partial class TotalTimeControl : UserControl
{
    public TotalTimeControl()
    {
        InitializeComponent();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TotalTime
    {
        get => lblTotalTime.Text;
        set => lblTotalTime.Text = value;
    }
}
