using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MusicForge.UI.Shared;

namespace MusicForge.UI.Shared;

/// <summary>
/// Scrollable read-only text viewer used for the change log and other
/// plain-text documents. Themed to match the app.
/// </summary>
public sealed class ScrollTextDialog : Form
{
    public ScrollTextDialog(string title, string content)
    {
        Text = title;
        var theme = ThemeManager.Current;
        BackColor = theme.WindowBackground;
        ForeColor = theme.TextPrimary;
        Font = ThemeApplier.MakeFont(theme);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 480);

        var box = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = theme.ControlDefaultSurface,
            ForeColor = theme.TextPrimary,
            Font = new Font("Consolas", 9.5F),
            BorderStyle = BorderStyle.None,
            Text = content
        };

        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Size = new Size(90, 28),
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            ForeColor = theme.TextPrimary,
            BackColor = theme.ControlDefaultSurface,
            UseVisualStyleBackColor = false
        };
        close.FlatAppearance.BorderColor = theme.BorderColor;
        close.FlatAppearance.MouseOverBackColor = theme.ControlHoverSurface;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = theme.WindowBackground };
        bottom.Controls.Add(close);
        bottom.Resize += (_, _) => close.Location = new Point(bottom.Width - close.Width - 12, 6);

        Controls.Add(box);
        Controls.Add(bottom);
        AcceptButton = close;
    }

    /// <summary>Shows the app change log from CHANGELOG.md next to the exe.</summary>
    public static void ShowChangeLog(IWin32Window? owner = null)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        string content;
        try
        {
            content = File.Exists(path)
                ? File.ReadAllText(path)
                : "No change log file found.";
        }
        catch (Exception ex)
        {
            content = $"Could not read change log:\n{ex.Message}";
        }
        using var dlg = new ScrollTextDialog("MusicForge - Change Log", content);
        if (owner is not null) dlg.ShowDialog(owner); else dlg.ShowDialog();
    }
}
