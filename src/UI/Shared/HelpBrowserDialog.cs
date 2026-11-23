using System.Drawing;
using System.Windows.Forms;
using MusicForge.UI.Data;
using MusicForge.UI.Shared;

namespace MusicForge.UI.Shared;

/// <summary>
/// In-app help browser: searchable contents list on the left, themed
/// article reader on the right. Articles come from the HelpTopics table.
/// </summary>
public sealed class HelpBrowserDialog : Form
{
    private sealed class ContentsItem
    {
        public string Label = "";
        public HelpRepository.Topic? Topic;
        public bool IsHeader => Topic is null;
    }

    private readonly ListBox _contents = new();
    private readonly RichTextBox _reader = new();
    private readonly TextBox _search = new();
    private readonly HelpRepository _repo = new();
    private bool _suppressSelection;

    public HelpBrowserDialog()
    {
        var theme = ThemeManager.Current;
        Text = "MusicForge Help";
        BackColor = theme.WindowBackground;
        ForeColor = theme.TextPrimary;
        Font = ThemeApplier.MakeFont(theme);
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(920, 620);
        MinimizeBox = false;

        _repo.GetTopics(); // seeds built-in articles on first run

        // ---- left pane ----
        var left = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = theme.ControlDefaultSurface };
        _search.PlaceholderText = "Search help…";
        _search.Dock = DockStyle.Top;
        _search.Height = 30;
        _search.BackColor = theme.ControlDefaultSurface;
        _search.ForeColor = theme.TextPrimary;
        _search.Font = Font;
        _search.TextChanged += (_, _) => RebuildContents();

        _contents.Dock = DockStyle.Fill;
        _contents.BackColor = theme.ControlDefaultSurface;
        _contents.ForeColor = theme.TextPrimary;
        _contents.Font = Font;
        _contents.BorderStyle = BorderStyle.None;
        _contents.DrawMode = DrawMode.OwnerDrawFixed;
        _contents.ItemHeight = 26;
        _contents.DrawItem += DrawContentsItem;
        _contents.SelectedIndexChanged += OnContentsSelected;

        left.Controls.Add(_contents);
        left.Controls.Add(_search);

        // ---- right pane ----
        _reader.Dock = DockStyle.Fill;
        _reader.ReadOnly = true;
        _reader.BackColor = theme.WindowBackground;
        _reader.ForeColor = theme.TextPrimary;
        _reader.Font = new Font("Segoe UI", 10F);
        _reader.BorderStyle = BorderStyle.None;
        _reader.WordWrap = true;

        Controls.Add(_reader);
        Controls.Add(left);

        RebuildContents();
        if (_contents.Items.Count > 1) _contents.SelectedIndex = 1; // first topic after header
    }

    private void RebuildContents()
    {
        _suppressSelection = true;
        _contents.Items.Clear();
        var ordered = _repo.Search(_search.Text);

        string? lastCategory = null;
        foreach (var t in ordered)
        {
            if (t.Category != lastCategory)
            {
                lastCategory = t.Category;
                _contents.Items.Add(new ContentsItem { Label = t.Category.ToUpperInvariant() });
            }
            _contents.Items.Add(new ContentsItem { Label = "   " + t.Title, Topic = t });
        }
        if (_contents.Items.Count == 0)
            _contents.Items.Add(new ContentsItem { Label = "   No topics match your search." });
        _suppressSelection = false;
    }

    private void DrawContentsItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _contents.Items.Count) return;
        var item = (ContentsItem)_contents.Items[e.Index];
        var theme = ThemeManager.Current;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        e.DrawBackground();
        var bounds = e.Bounds;
        if (item.IsHeader)
        {
            using var f = new Font(Font, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, item.Label, f, bounds, theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        else
        {
            Color fg = selected ? Color.White : theme.TextPrimary;
            Color bg = selected ? theme.ControlHoverSurface : theme.ControlDefaultSurface;
            using var b = new SolidBrush(bg);
            e.Graphics.FillRectangle(b, bounds);
            TextRenderer.DrawText(e.Graphics, item.Label, Font, bounds, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        e.DrawFocusRectangle();
    }

    private void OnContentsSelected(object? sender, EventArgs e)
    {
        if (_suppressSelection) return;
        if (_contents.SelectedItem is not ContentsItem { Topic: { } topic }) return;
        RenderTopic(topic);
    }

    private void RenderTopic(HelpRepository.Topic topic)
    {
        var theme = ThemeManager.Current;
        _reader.Clear();

        void Append(string text, Font? font, Color? color)
        {
            _reader.SelectionStart = _reader.TextLength;
            if (font is not null) _reader.SelectionFont = font;
            _reader.SelectionColor = color ?? theme.TextPrimary;
            _reader.AppendText(text + "\n");
        }

        using var h1 = new Font("Segoe UI", 15F, FontStyle.Bold);
        using var h2 = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        using var body = new Font("Segoe UI", 10F);
        using var bullet = new Font("Segoe UI", 10F);
        var accent = theme.PrimaryAccent;
        var secondary = theme.TextSecondary;

        Append(topic.Title, h1, accent);
        Append("", body, null);

        foreach (var raw in topic.Content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("# ")) { Append(line[2..], h1, accent); Append("", body, null); }
            else if (line.StartsWith("## ")) { Append(line[3..], h2, accent); }
            else if (line.StartsWith("- ")) Append("    • " + line[2..], bullet, theme.TextPrimary);
            else if (line.Length == 0) Append("", body, null);
            else Append(line, body, secondary);
        }
        _reader.SelectionStart = 0;
        _reader.ScrollToCaret();
    }

    public static void Open(IWin32Window? owner = null)
    {
        using var dlg = new HelpBrowserDialog();
        if (owner is not null) dlg.ShowDialog(owner); else dlg.ShowDialog();
    }
}
