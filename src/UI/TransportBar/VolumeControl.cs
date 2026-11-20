using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.TransportBar;

/// <summary>
/// Custom volume slider - WinForms TrackBar cannot use image thumbs, so the
/// track and the slider3.png thumb are owner-drawn. API matches the old
/// TrackBar version: <see cref="Volume"/> (0-100) + <see cref="VolumeChanged"/>.
/// </summary>
public partial class VolumeControl : UserControl
{
    /// <summary>Raised when the volume slider value changes (0-100).</summary>
    public event EventHandler<int>? VolumeChanged;

    private int _value = 75;
    private bool _dragging;
    private Image? _thumb;

    private const int TrackHeight = 6;
    private const int ThumbHeight = 36;
    private const int LabelWidth = 42;

    private static readonly Color TrackColor = Color.FromArgb(70, 70, 74);
    private static readonly Color FillColor = Color.FromArgb(120, 120, 126);
    private static readonly Color TextColor = Color.FromArgb(212, 212, 212);

    public VolumeControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(51, 51, 55);
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 8F);
        Size = new Size(180, 45);

        string thumbPath = Path.Combine(AppContext.BaseDirectory,
            "Assets", "Transport", "TransportBar", "slider3.png");
        if (File.Exists(thumbPath))
        {
            try { _thumb = Image.FromFile(thumbPath); }
            catch { _thumb = null; }
        }
    }

    /// <summary>Slider value 0-100.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Volume
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, 0, 100);
            if (v == _value) return;
            _value = v;
            Invalidate();
        }
    }

    private int TrackWidth => Math.Max(10, Width - LabelWidth - 8);
    private int TrackLeft => 4;
    private int TrackY => (Height - TrackHeight) / 2;

    private int ValueToX(int value) =>
        TrackLeft + (int)Math.Round((TrackWidth - 1) * (value / 100.0));

    private int XToValue(int x)
    {
        int clamped = Math.Clamp(x - TrackLeft, 0, TrackWidth - 1);
        return (int)Math.Round(clamped / (double)(TrackWidth - 1) * 100);
    }

    private void SetValueFromX(int x)
    {
        int v = XToValue(x);
        if (v == _value) return;
        _value = v;
        VolumeChanged?.Invoke(this, v);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && e.X < Width - LabelWidth)
        {
            _dragging = true;
            Capture = true;
            SetValueFromX(e.X);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) SetValueFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.X >= Width - LabelWidth) return;
        Volume = _value + (e.Delta > 0 ? 5 : -5);
        VolumeChanged?.Invoke(this, _value);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Track
        var trackRect = new Rectangle(TrackLeft, TrackY, TrackWidth, TrackHeight);
        using (var bg = new SolidBrush(TrackColor))
            g.FillRectangle(bg, trackRect);

        // Filled portion
        int fillW = Math.Max(TrackHeight, ValueToX(_value) - TrackLeft + 1);
        using (var fg = new SolidBrush(FillColor))
            g.FillRectangle(fg, new Rectangle(TrackLeft, TrackY, fillW, TrackHeight));

        // Thumb image centred on the value position
        int cx = ValueToX(_value);
        if (_thumb is not null)
        {
            // slider3.png is portrait - fit to thumb height, keep aspect
            float scale = (float)ThumbHeight / _thumb.Height;
            int tw = Math.Max(1, (int)(_thumb.Width * scale));
            var dest = new Rectangle(cx - tw / 2, (Height - ThumbHeight) / 2, tw, ThumbHeight);
            g.DrawImage(_thumb, dest);
        }
        else
        {
            // Fallback: plain ellipse thumb if the asset is missing
            using var b = new SolidBrush(Color.White);
            g.FillEllipse(b, cx - 5, TrackY - 4, 10, TrackHeight + 8);
        }

        // Percentage label at right
        TextRenderer.DrawText(g, $"{_value}%", Font, new Rectangle(Width - LabelWidth, 0, LabelWidth, Height),
            TextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _thumb?.Dispose();
        base.Dispose(disposing);
    }
}
