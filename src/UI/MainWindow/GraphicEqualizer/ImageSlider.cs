using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AudioForge.UI.MainWindow.GraphicEqualizer;

/// <summary>
/// Owner-drawn slider using the same slider3.png thumb as the transport
/// volume control. WinForms TrackBar cannot use image thumbs, so this
/// replicates the look in both orientations. API: <see cref="Value"/>,
/// <see cref="Minimum"/>, <see cref="Maximum"/>, <see cref="ValueChanged"/>.
/// </summary>
public sealed class ImageSlider : UserControl
{
    /// <summary>Raised when the user changes the value (not on programmatic set).</summary>
    public event EventHandler? ValueChanged;

    private int _value;
    private bool _dragging;
    private Image? _thumb;

    /// <summary>Thumb size on the slider's cross axis (travel-axis margin).</summary>
    private const int ThumbSize = 36;
    private const int TrackSize = 6;

    private static readonly Color TrackColor = Color.FromArgb(70, 70, 74);
    private static readonly Color FillColor = Color.FromArgb(120, 120, 126);

    public ImageSlider()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw
               | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Minimum = 0;
        Maximum = 100;
        WheelStep = 1;

        string thumbPath = Path.Combine(AppContext.BaseDirectory,
            "Assets", "Transport", "TransportBar", "slider3.png");
        if (File.Exists(thumbPath))
        {
            try { _thumb = Image.FromFile(thumbPath); }
            catch { _thumb = null; }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, Minimum, Maximum);
            if (v == _value) return;
            _value = v;
            Invalidate();
        }
    }

    /// <summary>Value change per mouse-wheel notch.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int WheelStep { get; set; } = 1;

    /// <summary>Thumb edge length in pixels on the cross axis. Defaults to
    /// 36 (volume-control size); band sliders use a smaller value.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ThumbPixels { get; set; } = ThumbSize;

    private bool Vertical => Orientation == Orientation.Vertical;

    private int Span => Math.Max(1, Maximum - Minimum);

    private int PosToValue(int pos, int trackStart, int trackLen)
    {
        float t = trackLen <= 1 ? 0f : Math.Clamp((pos - trackStart) / (float)(trackLen - 1), 0f, 1f);
        // Vertical: top = Maximum, bottom = Minimum (TrackBar convention)
        return Vertical
            ? Maximum - (int)Math.Round(t * Span)
            : Minimum + (int)Math.Round(t * Span);
    }

    private int ValueToPos(int value)
    {
        float t = (value - Minimum) / (float)Span;
        return Vertical
            ? (int)Math.Round((1f - t) * (TrackLen() - 1))
            : (int)Math.Round(t * (TrackLen() - 1));
    }

    private int TrackLen() => Vertical ? Math.Max(10, Height - ThumbSize) : Math.Max(10, Width - ThumbSize);
    private int TrackStart() => Vertical ? ThumbSize / 2 : ThumbSize / 2;
    private int TrackCross() => Vertical ? (Width - TrackSize) / 2 : (Height - TrackSize) / 2;

    private void SetValueFromPos(int pos, bool notify = true)
    {
        int v = PosToValue(pos, TrackStart(), TrackLen());
        if (v == _value) { Invalidate(); return; }
        _value = v;
        if (notify) ValueChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private bool HitTrack(Point p) =>
        Vertical
            ? p.X >= TrackCross() - 6 && p.X <= TrackCross() + TrackSize + 6
            : p.Y >= TrackCross() - 6 && p.Y <= TrackCross() + TrackSize + 6;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        Capture = true;
        int pos = Vertical ? e.Y : e.X;
        SetValueFromPos(pos);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) SetValueFromPos(Vertical ? e.Y : e.X);
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
        Value = _value + (e.Delta > 0 ? WheelStep : -WheelStep);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int pos = TrackStart() + ValueToPos(_value);
        int thumbCenter = Vertical ? pos : pos;

        if (Vertical)
        {
            var trackRect = new Rectangle(TrackCross(), TrackStart(), TrackSize, TrackLen());
            using (var bg = new SolidBrush(TrackColor)) g.FillRectangle(bg, trackRect);

            int fillBottom = TrackStart() + TrackLen() - 1;
            int fillTop = Math.Min(fillBottom, thumbCenter);
            using (var fg = new SolidBrush(FillColor))
                g.FillRectangle(fg, new Rectangle(TrackCross(), fillTop, TrackSize, Math.Max(1, fillBottom - fillTop + 1)));

            DrawThumb(g, thumbCenter);
        }
        else
        {
            var trackRect = new Rectangle(TrackStart(), TrackCross(), TrackLen(), TrackSize);
            using (var bg = new SolidBrush(TrackColor)) g.FillRectangle(bg, trackRect);

            int fillRight = TrackStart() + TrackLen() - 1;
            int fillW = Math.Max(TrackSize, thumbCenter - TrackStart() + 1);
            using (var fg = new SolidBrush(FillColor))
                g.FillRectangle(fg, new Rectangle(TrackStart(), TrackCross(), Math.Min(fillW, fillRight - TrackStart() + 1), TrackSize));

            DrawThumb(g, thumbCenter);
        }
    }

    private void DrawThumb(Graphics g, int center)
    {
        if (_thumb is not null)
        {
            // slider3.png is portrait - fit to ThumbSize on the cross axis
            float scale = Vertical
                ? (float)ThumbPixels / _thumb.Width
                : (float)ThumbPixels / _thumb.Height;
            int tw = Math.Max(1, (int)(_thumb.Width * scale));
            int th = Math.Max(1, (int)(_thumb.Height * scale));
            var dest = Vertical
                ? new Rectangle((Width - tw) / 2, center - th / 2, tw, th)
                : new Rectangle(center - tw / 2, (Height - th) / 2, tw, th);
            g.DrawImage(_thumb, dest);
        }
        else
        {
            using var b = new SolidBrush(Color.White);
            if (Vertical)
                g.FillEllipse(b, Width / 2 - 5, center - 7, 10, 14);
            else
                g.FillEllipse(b, center - 5, Height / 2 - 7, 10, 14);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _thumb?.Dispose();
        base.Dispose(disposing);
    }
}
