using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using MusicForge.UI.Shared;

namespace MusicForge.UI;

/// <summary>
/// Lightweight startup splash - app icon, name, and a status line.
/// Borderless, topmost, no taskbar button. Closed when MainForm is shown.
/// </summary>
internal sealed class SplashForm : Form
{
    private readonly Label _statusLabel;

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 320);
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = ThemeManager.Current.WindowBackground;
        ForeColor = ThemeManager.Current.TextPrimary;
        DoubleBuffered = true;

        // App icon, left side
        var iconBox = new PictureBox
        {
            Location = new Point(48, 96),
            Size = new Size(128, 128),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Main", "musicforge.ico");
            if (File.Exists(iconPath))
            {
                using var icon = new Icon(iconPath, new Size(128, 128));
                iconBox.Image = icon.ToBitmap();
            }
        }
        catch { /* icon is cosmetic */ }

        var nameLabel = new Label
        {
            Text = "MusicForge",
            Font = new Font("Segoe UI Semibold", 30F, FontStyle.Regular),
            ForeColor = ThemeManager.Current.TextPrimary,
            AutoSize = true,
            Location = new Point(200, 100),
            BackColor = Color.Transparent
        };

        var subtitleLabel = new Label
        {
            Text = "Music player  ·  AudioForge engine",
            Font = new Font("Segoe UI", 10F),
            ForeColor = ThemeManager.Current.TextSecondary,
            AutoSize = true,
            Location = new Point(202, 152),
            BackColor = Color.Transparent
        };

        // Thin accent rule under the title
        var rule = new Panel
        {
            Location = new Point(202, 186),
            Size = new Size(280, 2),
            BackColor = ThemeManager.Current.PrimaryAccent
        };

        _statusLabel = new Label
        {
            Text = "Loading…",
            Font = new Font("Segoe UI", 9F),
            ForeColor = ThemeManager.Current.TextSecondary,
            AutoSize = true,
            Location = new Point(202, 210),
            BackColor = Color.Transparent
        };

        var versionLabel = new Label
        {
            Text = $"v{GetVersion()}",
            Font = new Font("Segoe UI", 8F),
            ForeColor = ThemeManager.Current.TextDisabled,
            AutoSize = true,
            Location = new Point(202, 282),
            BackColor = Color.Transparent
        };

        Controls.AddRange(new Control[] { iconBox, nameLabel, subtitleLabel, rule, _statusLabel, versionLabel });
    }

    /// <summary>Update the status line while startup work runs.</summary>
    public void SetStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => _statusLabel.Text = text); return; }
        _statusLabel.Text = text;
    }

    private static string GetVersion()
    {
        var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        return v is null ? "1.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Soft border so the borderless window reads as a card on any wallpaper
        using var pen = new Pen(ThemeManager.Current.BorderColor, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
