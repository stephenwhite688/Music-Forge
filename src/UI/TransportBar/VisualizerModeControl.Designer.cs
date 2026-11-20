namespace AudioForge.UI.TransportBar;

partial class VisualizerModeControl
{
    private Button btnMode;

    private void InitializeComponent()
    {
        btnMode = new Button();

        SuspendLayout();

        btnMode.Dock = DockStyle.Fill;
        btnMode.Text = "Bars";
        btnMode.FlatStyle = FlatStyle.Flat;
        btnMode.FlatAppearance.BorderSize = 0;
        btnMode.Click += btnMode_Click;

        Controls.Add(btnMode);

        // Change these two values to resize the Bars/Scope button.
        Size = new System.Drawing.Size(42,24);

        ResumeLayout(false);
    }
}
