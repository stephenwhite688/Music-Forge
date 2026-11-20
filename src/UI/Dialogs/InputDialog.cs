using System.Drawing;
using System.Windows.Forms;

namespace AudioForge.UI.Dialogs;

/// <summary>
/// Minimal modal text-input dialog (WinForms has no built-in InputBox).
/// </summary>
public static class InputDialog
{
    /// <summary>Shows a prompt; returns the entered text or null if cancelled.</summary>
    public static string? Show(IWin32Window? owner, string prompt, string title = "Input", string initial = "")
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(360, 120),
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        var label = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true, ForeColor = Color.White };
        var textBox = new TextBox
        {
            Text = initial,
            Location = new Point(12, 36),
            Width = 336,
            BackColor = Color.FromArgb(30, 30, 32),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(190, 72), Width = 75 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(273, 72), Width = 75 };

        form.Controls.AddRange(new Control[] { label, textBox, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : null;
    }
}
