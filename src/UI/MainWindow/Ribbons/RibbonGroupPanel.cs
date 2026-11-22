using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicForge.UI.MainWindow.Ribbons;

/// <summary>
/// A labeled ribbon group: a row of ribbon-style buttons (icon area on top,
/// text label underneath) across the top and a caption label along the bottom.
/// </summary>
public partial class RibbonGroupPanel : Panel
{
    private const int SlotGap = 4;
    private const int ContentHeight = 42;
    private const int CaptionHeight = 13;

    private readonly Panel _contentArea;
    private readonly Label _captionLabel;
    private int _nextX = 6;

    /// <summary>Gets or sets the group caption shown along the bottom edge.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Caption
    {
        get => _captionLabel.Text;
        set => _captionLabel.Text = value ?? string.Empty;
    }

    public RibbonGroupPanel(string caption)
    {
        InitializeComponent();

        Height = 55;
        BackColor = Color.FromArgb(51, 51, 55);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        _contentArea = new Panel
        {
            Location = new Point(0, 0),
            Height = ContentHeight,
            BackColor = Color.Transparent
        };

        _captionLabel = new Label
        {
            Location = new Point(0, ContentHeight),
            Height = CaptionHeight,
            Text = caption,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 7.5F)
        };

        Controls.Add(_contentArea);
        Controls.Add(_captionLabel);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Right-edge separator so groups read as distinct sections
        using var pen = new Pen(Color.FromArgb(35, 35, 38));
        e.Graphics.DrawLine(pen, Width - 1, 4, Width - 1, Height - 4);
    }

    public Control AddControl(Control control)
    {
        control.Location = new Point(_nextX, (ContentHeight - control.Height) / 2);
        _contentArea.Controls.Add(control);
        _nextX += control.Width + SlotGap;
        UpdateWidth();
        return control;
    }

    /// <summary>
    /// Adds a ribbon-style button: image area on top, text label underneath.
    /// If <paramref name="onClick"/> is supplied it runs on left-click; otherwise
    /// left-click drops the supplied menu down. Right-click always shows the menu.
    /// </summary>
    public Button AddButton(string text, ContextMenuStrip? menu = null, Image? image = null, Action? onClick = null)
    {
        var button = new Button
        {
            Size = new Size(72, 40),
            Text = text,
            Image = image,
            Font = new Font("Segoe UI", 7F),
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.BottomCenter,
            ImageAlign = ContentAlignment.TopCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 62);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 75);

        if (menu != null)
            button.ContextMenuStrip = menu;

        if (onClick != null)
            button.Click += (s, e) => onClick();
        else if (menu != null)
            button.Click += (s, e) => menu.Show(button, new Point(0, button.Height));

        return (Button)AddControl(button);
    }

    private void UpdateWidth()
    {
        Width = Math.Max(90, _nextX + 2);
        _contentArea.Width = Width;
        _contentArea.Height = ContentHeight;
        _captionLabel.Width = Width;
    }
}
