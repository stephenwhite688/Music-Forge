using AudioForge.UI.Services;
using AudioForge.Visualizer.Contracts;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AudioForge.UI.Visualiser;

/// <summary>Available realtime display modes.</summary>
public enum VizMode
{
    Bars,
    Oscilloscope,
    Spectrum,
    Spectrogram,
    Waveform,
    Loudness
}

/// <summary>Colour themes for the display.</summary>
public enum VizTheme
{
    Dark,
    HighContrast,
    Heat,
    Neon,
    Grayscale
}

/// <summary>
/// Professional realtime visualiser surface. Polls the engine's tap nodes
/// (spectrum, waveform, spectrogram, meters) on a UI timer and renders the
/// selected mode. All data comes from the live playback chain - nothing is
/// simulated.
/// </summary>
public class VisualiserDisplayControl : UserControl
{
    private readonly System.Windows.Forms.Timer _timer;
    private VizMode _mode = VizMode.Bars;
    private VizTheme _theme = VizTheme.Dark;
    private bool _running = true;
    private bool _showOverlays = true;
    private int _bandCount = 48;
    private bool _logFreq = true;
    private bool _peakHold = true;

    // Peak-hold state for spectrum/bars
    private float[]? _peakHoldDb;

    private IVisualiserPlugin? _plugin;
    private readonly System.Diagnostics.Stopwatch _pluginClock = System.Diagnostics.Stopwatch.StartNew();
    private long _frameIndex;

    public VisualiserDisplayControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(18, 18, 20);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 8F);

        _timer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30 fps
        _timer.Tick += (_, _) => Invalidate();
        _timer.Start();
    }

    /// <summary>Active display mode.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public VizMode Mode { get => _mode; set { _mode = value; Invalidate(); } }

    /// <summary>Colour theme.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public VizTheme Theme { get => _theme; set { _theme = value; Invalidate(); } }

    /// <summary>Start/stop the refresh timer (visualiser on/off).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Running { get => _running; set { _running = value; if (value) _timer.Start(); else _timer.Stop(); } }

    /// <summary>Draw metadata overlays (track info, levels).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowOverlays { get => _showOverlays; set { _showOverlays = value; Invalidate(); } }

    /// <summary>Number of bars/bands in Bars and Spectrum modes (31-128).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int BandCount { get => _bandCount; set { _bandCount = Math.Clamp(value, 8, 256); Invalidate(); } }

    /// <summary>Logarithmic frequency spacing for Bars/Spectrum.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool LogFrequency { get => _logFreq; set { _logFreq = value; Invalidate(); } }

    /// <summary>Peak-hold markers on Bars/Spectrum.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool PeakHoldEnabled { get => _peakHold; set { _peakHold = value; Invalidate(); } }

    /// <summary>
    /// Active plugin effect. When set, it takes over rendering instead of the
    /// built-in modes. Set to null to return to built-in modes.
    /// </summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IVisualiserPlugin? Plugin
    {
        get => _plugin;
        set
        {
            if (ReferenceEquals(_plugin, value)) return;
            _plugin?.OnDeactivated();
            _plugin = value;
            _plugin?.OnActivated();
            _frameIndex = 0;
            _pluginClock.Restart();
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackgroundColor);

        if (!_running) { DrawCentered(g, "Visualiser stopped"); return; }

        var meters = MeterService.Instance;

        if (_plugin is not null)
        {
            _plugin.Draw(g, BuildFrame(meters));
            _frameIndex++;
            return;
        }

        switch (_mode)
        {
            case VizMode.Bars: DrawBars(g, meters); break;
            case VizMode.Oscilloscope: DrawOscilloscope(g, meters); break;
            case VizMode.Spectrum: DrawSpectrumLine(g, meters); break;
            case VizMode.Spectrogram: DrawSpectrogram(g, meters); break;
            case VizMode.Waveform: DrawWaveform(g, meters); break;
            case VizMode.Loudness: DrawLoudness(g, meters); break;
        }

        if (_showOverlays) DrawOverlays(g, meters);
    }

    private VisualFrame BuildFrame(MeterService meters)
    {
        var mixer = meters.Mixer;
        return new VisualFrame
        {
            SpectrumDb = meters.PostEq.SpectrumDb,
            BinWidthHz = meters.PostEq.BinWidthHz,
            WaveformLeft = meters.Waveform.SnapshotChannel(0, ClientRectangle.Width),
            WaveformRight = meters.Waveform.SnapshotChannel(1, ClientRectangle.Width),
            RmsDb = mixer.RmsDbPerChannel,
            PeakDb = mixer.PeakDbPerChannel,
            SampleRate = 48000,
            Width = ClientRectangle.Width,
            Height = ClientRectangle.Height,
            TimeSeconds = _pluginClock.Elapsed.TotalSeconds,
            FrameIndex = _frameIndex,
            HasAudio = meters.PostEq.HasData
        };
    }

    // ---------------- themes ----------------

    private Color BackgroundColor => _theme switch
    {
        VizTheme.HighContrast => Color.Black,
        VizTheme.Grayscale => Color.FromArgb(24, 24, 24),
        _ => Color.FromArgb(18, 18, 20)
    };

    private Color AccentColor => _theme switch
    {
        VizTheme.HighContrast => Color.Lime,
        VizTheme.Heat => Color.OrangeRed,
        VizTheme.Neon => Color.Cyan,
        VizTheme.Grayscale => Color.White,
        _ => Color.LightSkyBlue
    };

    private Color GridColor => _theme == VizTheme.HighContrast
        ? Color.FromArgb(60, 60, 60)
        : Color.FromArgb(45, 45, 50);

    private static float DbToNorm(float db, float floor = -90f, float ceil = 0f)
        => Math.Clamp((db - floor) / (ceil - floor), 0f, 1f);

    // ---------------- modes ----------------

    private void DrawBars(Graphics g, MeterService meters)
    {
        var spec = meters.PostEq.SpectrumDb;
        if (spec.Length == 0) { DrawCentered(g, "No audio data"); return; }
        EnsurePeakHold(spec.Length);
        float binHz = meters.PostEq.BinWidthHz;
        int bands = _bandCount;
        float barW = (float)ClientRectangle.Width / bands;
        var accent = AccentColor;

        using var barBrush = new SolidBrush(accent);
        using var peakPen = new Pen(Color.OrangeRed, 1f);
        using var gridPen = new Pen(GridColor);

        for (int b = 0; b < bands; b++)
        {
            // Map band to bin range (log or linear)
            int bin0, bin1;
            if (_logFreq)
            {
                double f0 = 20.0 * Math.Pow(20000.0 / 20.0, (double)b / bands);
                double f1 = 20.0 * Math.Pow(20000.0 / 20.0, (double)(b + 1) / bands);
                bin0 = Math.Clamp((int)(f0 / binHz), 0, spec.Length - 1);
                bin1 = Math.Clamp((int)(f1 / binHz), bin0 + 1, spec.Length);
            }
            else
            {
                bin0 = b * spec.Length / bands;
                bin1 = (b + 1) * spec.Length / bands;
            }
            float maxDb = -120f;
            for (int i = bin0; i < bin1; i++) maxDb = Math.Max(maxDb, spec[i]);

            float h = DbToNorm(maxDb) * (ClientRectangle.Height - 20);
            float x = b * barW + 1;
            g.FillRectangle(barBrush, x, ClientRectangle.Height - 14 - h, Math.Max(1, barW - 2), h);

            if (_peakHold && _peakHoldDb != null)
            {
                int pb = Math.Clamp((bin0 + bin1) / 2, 0, _peakHoldDb.Length - 1);
                float ph = DbToNorm(_peakHoldDb[pb]) * (ClientRectangle.Height - 20);
                _peakHoldDb[pb] = maxDb > _peakHoldDb[pb] ? maxDb : _peakHoldDb[pb] * 0.995f;
                g.DrawLine(peakPen, x, ClientRectangle.Height - 14 - ph, x + barW - 2, ClientRectangle.Height - 14 - ph);
            }
        }
        // baseline
        g.DrawLine(gridPen, 0, ClientRectangle.Height - 14, ClientRectangle.Width, ClientRectangle.Height - 14);
    }

    private void EnsurePeakHold(int bins)
    {
        if (_peakHoldDb == null || _peakHoldDb.Length != bins)
            _peakHoldDb = new float[bins];
    }

    private void DrawOscilloscope(Graphics g, MeterService meters)
    {
        int w = ClientRectangle.Width, h = ClientRectangle.Height;
        using var gridPen = new Pen(GridColor);
        g.DrawLine(gridPen, 0, h / 2, w, h / 2);

        DrawChannelWave(g, meters.Waveform.SnapshotChannel(0, w), 0, h / 2 - 4, AccentColor);
        DrawChannelWave(g, meters.Waveform.SnapshotChannel(1, w), 0, h / 2 + 4, Color.OrangeRed);
    }

    private void DrawChannelWave(Graphics g, float[] samples, int x0, int midY, Color color)
    {
        if (samples.Length < 2) return;
        using var pen = new Pen(color, 1.2f);
        float scale = 40f;
        for (int x = 1; x < samples.Length && x0 + x < ClientRectangle.Width; x++)
        {
            int y0 = midY - (int)(samples[x - 1] * scale);
            int y1 = midY - (int)(samples[x] * scale);
            g.DrawLine(pen, x0 + x - 1, y0, x0 + x, y1);
        }
    }

    private void DrawSpectrumLine(Graphics g, MeterService meters)
    {
        var spec = meters.PostEq.SpectrumDb;
        if (spec.Length == 0) { DrawCentered(g, "No audio data"); return; }
        EnsurePeakHold(spec.Length);
        int w = ClientRectangle.Width, h = ClientRectangle.Height - 14;
        using var pen = new Pen(AccentColor, 1.4f);
        using var peakPen = new Pen(Color.OrangeRed, 1f);
        using var gridPen = new Pen(GridColor);

        // log-frequency grid lines at 100Hz/1k/10k
        float binHz = meters.PostEq.BinWidthHz;
        foreach (var f in new[] { 100f, 1000f, 10000f })
        {
            int bin = (int)(f / binHz);
            if (bin >= 0 && bin < spec.Length)
            {
                float x = _logFreq ? LogX(bin, spec.Length, w) : (float)bin / spec.Length * w;
                g.DrawLine(gridPen, x, 0, x, h);
            }
        }

        PointF? prev = null;
        PointF? prevPeak = null;
        for (int i = 0; i < spec.Length; i++)
        {
            float x = _logFreq ? LogX(i, spec.Length, w) : (float)i / spec.Length * w;
            float y = h - DbToNorm(spec[i]) * h;
            var pt = new PointF(x, y);
            if (prev.HasValue) g.DrawLine(pen, prev.Value, pt);
            prev = pt;

            if (_peakHold)
            {
                float ph = h - DbToNorm(_peakHoldDb![i]) * h;
                var pp = new PointF(x, ph);
                if (prevPeak.HasValue) g.DrawLine(peakPen, prevPeak.Value, pp);
                prevPeak = pp;
                _peakHoldDb[i] = spec[i] > _peakHoldDb[i] ? spec[i] : _peakHoldDb[i] * 0.998f;
            }
        }
        g.DrawLine(gridPen, 0, h, w, h);
    }

    private static float LogX(int bin, int bins, int width)
    {
        // map bin index (linear freq) to log-frequency x position 20Hz-20kHz
        double f = bin; // relative position; true Hz handled by caller binHz - approximation ok for display
        return (float)(Math.Log10(Math.Max(1, bin + 1)) / Math.Log10(bins)) * width;
    }

    private void DrawSpectrogram(Graphics g, MeterService meters)
    {
        var snap = meters.Spectrogram.Snapshot();
        int sets = meters.Spectrogram.StoredChannels;
        int cols = meters.Spectrogram.ColumnCount;
        int bins = meters.Spectrogram.BinCount;
        if (cols == 0 || snap.Length == 0) { DrawCentered(g, "Building spectrogram…"); return; }

        int w = ClientRectangle.Width, h = ClientRectangle.Height;
        int bandH = h / Math.Max(1, sets);
        float dbFloor = -90f, dbCeil = 0f;

        for (int s = 0; s < sets; s++)
        {
            for (int c = 0; c < cols; c++)
            {
                int x = w - 1 - (int)((long)c * w / Math.Max(1, cols)); // newest at right
                for (int b = 0; b < bins; b++)
                {
                    // freq vertical: low freq at bottom
                    int yInBand = bandH - 1 - (int)((long)b * bandH / bins);
                    float db = snap[(s * cols + c) * bins + b];
                    float t = DbToNorm(db, dbFloor, dbCeil);
                    if (t <= 0.01f) continue;
                    int y = s * bandH + yInBand;
                    Color col = SpectrumColor(t);
                    using var brush = new SolidBrush(Color.FromArgb((int)(60 + 195 * t), col));
                    g.FillRectangle(brush, x, y, 1, 1);
                }
            }
        }
        // channel labels
        using var labelFont = new Font("Segoe UI", 8F, FontStyle.Bold);
        using var labelBrush = new SolidBrush(AccentColor);
        for (int s = 0; s < sets; s++)
            g.DrawString(s == 0 ? "L" : s == 1 ? "R" : $"Ch{s + 1}", labelFont, labelBrush, 4, s * bandH + 4);
    }

    private Color SpectrumColor(float t) => _theme switch
    {
        VizTheme.Heat => Color.FromArgb(255, (int)(255 * t), 0),
        VizTheme.Neon => Color.FromArgb(0, (int)(255 * t), 255),
        VizTheme.Grayscale => Color.FromArgb((int)(255 * t), (int)(255 * t), (int)(255 * t)),
        VizTheme.HighContrast => Color.FromArgb((int)(255 * t), (int)(255 * t), 0),
        _ => Color.FromArgb((int)(80 + 175 * t), (int)(40 + 100 * t), (int)(120 + 135 * t))
    };

    private void DrawWaveform(Graphics g, MeterService meters)
    {
        int w = ClientRectangle.Width, h = ClientRectangle.Height;
        using var gridPen = new Pen(GridColor);
        for (int i = 1; i < 4; i++)
            g.DrawLine(gridPen, 0, h * i / 4, w, h * i / 4);

        DrawWavePeaks(g, meters.Waveform.SnapshotPeaks(0, w / 2), 0, h / 2 - 30, AccentColor);
        DrawWavePeaks(g, meters.Waveform.SnapshotPeaks(1, w / 2), 0, h / 2 + 30, Color.OrangeRed);
        g.DrawString("L", Font, Brushes.Gainsboro, 4, h / 2 - 44);
        g.DrawString("R", Font, Brushes.Gainsboro, 4, h / 2 + 16);
    }

    private void DrawWavePeaks(Graphics g, float[] peaks, int x0, int midY, Color color)
    {
        if (peaks.Length < 2) return;
        int blocks = peaks.Length / 2;
        float scale = 60f;
        using var pen = new Pen(color, 1f);
        float bw = (float)ClientRectangle.Width / blocks;
        for (int b = 0; b < blocks; b++)
        {
            float min = peaks[b * 2], max = peaks[b * 2 + 1];
            int yTop = midY - (int)(max * scale);
            int yBot = midY - (int)(min * scale);
            g.DrawLine(pen, x0 + b * bw, yTop, x0 + (b + 1) * bw, yTop);
            g.DrawLine(pen, x0 + b * bw, yBot, x0 + (b + 1) * bw, yBot);
        }
    }

    private void DrawLoudness(Graphics g, MeterService meters)
    {
        int w = ClientRectangle.Width, h = ClientRectangle.Height;
        var output = meters.Output;
        var tp = meters.TruePeak;

        // Vertical level bars: RMS, Peak-hold, True-peak
        DrawLevelBar(g, 20, 10, 30, h - 20, output.RmsDb, -60, 0, Color.DodgerBlue, "RMS");
        DrawLevelBar(g, w / 3, 10, 30, h - 20, output.HeldPeakDb, -60, 0, AccentColor, "Peak");
        DrawLevelBar(g, 2 * w / 3, 10, 30, h - 20, tp.HeldPeakDb, -60, 0, Color.OrangeRed, "TruePk");

        // dB scale labels
        using var font = new Font("Consolas", 8F);
        using var brush = new SolidBrush(Color.Gainsboro);
        for (int db = -60; db <= 0; db += 12)
        {
            float y = 10 + (h - 20) * (1f - DbToNorm(db, -60, 0));
            g.DrawString($"{db}", font, brush, 4, y - 7);
            using var gp = new Pen(GridColor);
            g.DrawLine(gp, 50, y, w - 10, y);
        }
        if (output.Clip || tp.Clip)
        {
            using var warn = new SolidBrush(Color.Red);
            g.DrawString("CLIP", new Font("Segoe UI", 12F, FontStyle.Bold), warn, w / 2 - 20, 8);
        }
    }

    private void DrawLevelBar(Graphics g, int x, int y, int barW, int barH, float db, float floorDb, float ceilDb, Color color, string label)
    {
        float t = DbToNorm(db, floorDb, ceilDb);
        int fillH = (int)(barH * t);
        using var bg = new SolidBrush(Color.FromArgb(40, 40, 44));
        using var fg = new SolidBrush(color);
        using var pen = new Pen(GridColor);
        g.FillRectangle(bg, x, y, barW, barH);
        g.FillRectangle(fg, x, y + barH - fillH, barW, fillH);
        g.DrawRectangle(pen, x, y, barW, barH);
        g.DrawString($"{db:F1}", Font, Brushes.Gainsboro, x - 4, y + barH + 2);
        g.DrawString(label, Font, Brushes.Gainsboro, x - 8, y - 14);
    }

    private void DrawOverlays(Graphics g, MeterService meters)
    {
        var output = meters.Output;
        var tp = meters.TruePeak;
        using var font = new Font("Segoe UI", 8F);
        using var brush = new SolidBrush(Color.Gainsboro);
        int y = 4;
        g.DrawString($"Out RMS {output.RmsDb:F1} dB  Peak {output.HeldPeakDb:F1} dB  TruePk {tp.HeldPeakDb:F1} dBTP", font, brush, 6, y);
        y += 14;
        g.DrawString($"Mode: {_mode}   Theme: {_theme}   {(_logFreq ? "Log" : "Lin")} freq   {(PeakHoldEnabled ? "Peak-hold" : "")}", font, brush, 6, y);
    }

    private void DrawCentered(Graphics g, string text)
    {
        using var font = new Font("Segoe UI", 10F, FontStyle.Italic);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, Brushes.Gray,
            (ClientRectangle.Width - size.Width) / 2, (ClientRectangle.Height - size.Height) / 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer?.Dispose();
        base.Dispose(disposing);
    }
}
