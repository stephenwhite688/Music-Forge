namespace MusicForge.UI.TransportBar;

partial class VisualizerPowerControl
{
    private Button btnPower;

    private void InitializeComponent()
    {
        btnPower = new Button();

        SuspendLayout();

        btnPower.Dock = DockStyle.Fill;
        btnPower.Text = "On";
        btnPower.FlatStyle = FlatStyle.Flat;
        btnPower.FlatAppearance.BorderSize = 0;
        btnPower.Click += btnPower_Click;

        Controls.Add(btnPower);

        // Change these two values to resize the On/Off button.
        Size = new System.Drawing.Size(42,24);

        ResumeLayout(false);
    }
}
