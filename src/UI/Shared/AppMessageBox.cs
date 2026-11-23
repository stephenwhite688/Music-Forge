using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MusicForge.UI.Shared;

namespace MusicForge.UI.Shared;

/// <summary>
/// Themed replacement for <see cref="MessageBox"/> - dark card, drawn
/// severity glyph, theme colours, same DialogResult semantics so call sites
/// only need the class name swapped.
/// </summary>
public static class AppMessageBox
{
    public static DialogResult Show(string text)
        => Show(text, "MusicForge", MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption)
        => Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        => Show(text, caption, buttons, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons,
        MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => Show(null, text, caption, buttons, icon, defaultButton);

    public static DialogResult Show(IWin32Window? owner, string text)
        => Show(owner, text, "MusicForge", MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(IWin32Window? owner, string text, string caption)
        => Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons)
        => Show(owner, text, caption, buttons, MessageBoxIcon.None);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons, MessageBoxIcon icon)
        => Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(IWin32Window? owner, string text, string caption,
        MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        using var dlg = new MessageDialog(text, caption, buttons, icon, defaultButton);
        if (owner is not null)
            return dlg.ShowDialog(owner);
        return dlg.ShowDialog();
    }

    private sealed class MessageDialog : Form
    {
        private readonly string _text;
        private readonly MessageBoxIcon _icon;
        private readonly DialogResult _result = DialogResult.None;

        public MessageDialog(string text, string caption, MessageBoxButtons buttons,
            MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            _text = text;
            _icon = icon;
            Text = caption;

            var theme = ThemeManager.Current;
            BackColor = theme.WindowBackground;
            ForeColor = theme.TextPrimary;
            Font = ThemeApplier.MakeFont(theme);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            DoubleBuffered = true;

            // Measure text to size the dialog
            using var measure = CreateGraphics();
            var textSize = TextRenderer.MeasureText(text, Font,
                new Size(460, int.MaxValue), TextFormatFlags.WordBreak);
            int width = Math.Clamp(textSize.Width + 150, 320, 560);
            int height = Math.Clamp(textSize.Height + 130, 150, 480);
            ClientSize = new Size(width, height);

            var iconPanel = new GlyphPanel(icon)
            {
                Location = new Point(20, 20),
                Size = new Size(36, 36)
            };

            var messageLabel = new Label
            {
                Text = text,
                Location = new Point(66, 22),
                Size = new Size(width - 86, height - 120),
                ForeColor = theme.TextPrimary,
                BackColor = Color.Transparent
            };

            Controls.Add(iconPanel);
            Controls.Add(messageLabel);
            AddButtons(buttons, defaultButton, theme);
        }

        private void AddButtons(MessageBoxButtons buttons, MessageBoxDefaultButton def, ThemeModel theme)
        {
            var specs = buttons switch
            {
                MessageBoxButtons.OK => new[] { ("OK", DialogResult.OK) },
                MessageBoxButtons.OKCancel => new[] { ("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel) },
                MessageBoxButtons.YesNo => new[] { ("Yes", DialogResult.Yes), ("No", DialogResult.No) },
                MessageBoxButtons.YesNoCancel => new[] { ("Yes", DialogResult.Yes), ("No", DialogResult.No), ("Cancel", DialogResult.Cancel) },
                MessageBoxButtons.RetryCancel => new[] { ("Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel) },
                MessageBoxButtons.AbortRetryIgnore => new[] { ("Abort", DialogResult.Abort), ("Retry", DialogResult.Retry), ("Ignore", DialogResult.Ignore) },
                _ => new[] { ("OK", DialogResult.OK) }
            };

            int x = ClientSize.Width - 16;
            Button? firstDefault = null;
            var created = new List<Button>();
            foreach (var (label, result) in specs.Reverse())
            {
                var btn = new Button
                {
                    Text = label,
                    DialogResult = result,
                    Size = new Size(88, 28),
                    FlatStyle = FlatStyle.Flat,
                    Font = Font,
                    ForeColor = theme.TextPrimary,
                    BackColor = theme.ControlDefaultSurface,
                    UseVisualStyleBackColor = false
                };
                btn.FlatAppearance.BorderColor = theme.BorderColor;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = theme.ControlHoverSurface;
                btn.FlatAppearance.MouseDownBackColor = theme.ControlPressedSurface;
                x -= btn.Width + 8;
                btn.Location = new Point(x, ClientSize.Height - 42);
                Controls.Add(btn);
                created.Add(btn);
            }

            // Default button: first affirmative per MessageBoxDefaultButton
            int defaultIndex = def switch
            {
                MessageBoxDefaultButton.Button2 => Math.Min(1, specs.Length - 1),
                MessageBoxDefaultButton.Button3 => Math.Min(2, specs.Length - 1),
                _ => specs.Length - 1 // Button1 = rightmost affirmative in WinForms order
            };
            // specs are laid out right-to-left; default maps to the last-created matching position
            var defaultBtn = created[Math.Clamp(created.Count - 1 - defaultIndex, 0, created.Count - 1)];
            AcceptButton = defaultBtn;
            firstDefault = defaultBtn;

            // Esc cancels when a Cancel result exists
            var cancelBtn = created.FirstOrDefault(b => b.DialogResult == DialogResult.Cancel);
            if (cancelBtn is not null) CancelButton = cancelBtn;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Thin accent top edge, like a modern dialog card
            var theme = ThemeManager.Current;
            using var brush = new SolidBrush(theme.PrimaryAccent);
            e.Graphics.FillRectangle(brush, 0, 0, Width, 2);
        }

        /// <summary>Draws a simple severity glyph (i / ! / ? / x) in a circle.</summary>
        private sealed class GlyphPanel : Panel
        {
            private readonly MessageBoxIcon _icon;

            public GlyphPanel(MessageBoxIcon icon) => _icon = icon;

            protected override void OnPaint(PaintEventArgs e)
            {
                var theme = ThemeManager.Current;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color fill, glyph;
                switch (_icon)
                {
                    case MessageBoxIcon.Warning:
                        fill = Color.FromArgb(230, 160, 40); glyph = Color.FromArgb(40, 26, 4); break;
                    case MessageBoxIcon.Error:
                        fill = Color.FromArgb(210, 60, 60); glyph = Color.White; break;
                    case MessageBoxIcon.Question:
                        fill = theme.PrimaryAccent; glyph = Color.White; break;
                    default:
                        fill = Color.FromArgb(70, 140, 220); glyph = Color.White; break;
                }

                var rect = new Rectangle(1, 1, Width - 3, Height - 3);
                using (var b = new SolidBrush(fill))
                    g.FillEllipse(b, rect);
                using (var p = new Pen(glyph, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float cx = Width / 2f, cy = Height / 2f;
                    switch (_icon)
                    {
                        case MessageBoxIcon.Warning:
                            g.DrawLine(p, cx, cy - 9, cx, cy + 2);
                            g.FillEllipse(new SolidBrush(glyph), cx - 1.6f, cy + 6.5f, 3.2f, 3.2f);
                            break;
                        case MessageBoxIcon.Error:
                            g.DrawLine(p, cx - 7, cy - 7, cx + 7, cy + 7);
                            g.DrawLine(p, cx + 7, cy - 7, cx - 7, cy + 7);
                            break;
                        case MessageBoxIcon.Question:
                            using (var f = new Font("Segoe UI", 15f, FontStyle.Bold))
                                TextRenderer.DrawText(g, "?", f, new Rectangle(0, 0, Width, Height),
                                    glyph, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                            break;
                        default:
                            g.DrawLine(p, cx - 2, cy - 9, cx - 2, cy + 1);
                            g.FillEllipse(new SolidBrush(glyph), cx - 2.4f, cy + 5f, 4.8f, 4.8f);
                            break;
                    }
                }
            }
        }
    }
}
