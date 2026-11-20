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
            float x = _logFreq ? LogX(f, w) : (float)(f / binHz) / spec.Length * w;
            if (x >= 0 && x <= w)
            {
                g.DrawLine(gridPen, x, 0, x, h);
            }
        }

        PointF? prev = null;
        PointF? prevPeak = null;
        for (int i = 0; i < spec.Length; i++)
        {
            float x = _logFreq ? LogX(i * binHz, w) : (float)i / spec.Length * w;
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

    private static float LogX(float freqHz, int width)
    {
        // True log-frequency mapping: 20 Hz -> 20 kHz across the width
        const float fmin = 20f, fmax = 20000f;
        float t = (float)(Math.Log10(Math.Max(freqHz, fmin)) - Math.Log10(fmin))
                / (float)(Math.Log10(fmax) - Math.Log10(fmin));
        return Math.Clamp(t, 0f, 1f) * width;
    }

    private void DrawSpectrogram(Graphics g, MeterService meters)
    {
        // Snapshot and dims captured atomically - FFT reconfig can't race us.
        var (sets, cols, bins, snap) = meters.Spectrogram.SnapshotDetailed();
        if (cols == 0 || snap.Length == 0) { DrawCentered(g, "Building spectrogram…"); return; }

        int w = ClientRectangle.Width, h = ClientRectangle.Height;
        int bandH = Math.Max(1, h / Math.Max(1, sets));
        const float dbFloor = -90f, dbCeil = 0f;

        // Pre-quantized brush palette - one brush per intensity level instead
        // of allocating a brush per pixel (8192-pt FFT = 4096 bins/column).
        const int levels = 80;
        var palette = new SolidBrush[levels];
        for (int i = 0; i < levels; i++)
        {
            float t = i / (float)(levels - 1);
            Color col = SpectrumColor(t);
            palette[i] = new SolidBrush(Color.FromArgb((int)(60 + 195 * t), col));
        }

        var rowMax = new float[bandH];
        try
        {
            for (int s = 0; s < sets; s++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int x = w - 1 - (int)((long)c * w / Math.Max(1, cols)); // newest at right
                    Array.Clear(rowMax);

                    // Fold bins into pixel rows keeping the max dB per row -
                    // bins >> pixels at large FFT sizes.
                    int colBase = (s * cols + c) * bins;
                    for (int b = 0; b < bins; b++)
                    {
                        int yInBand = bandH - 1 - (int)((long)b * bandH / bins);
                        if (yInBand < 0) yInBand = 0; else if (yInBand >= bandH) yInBand = bandH - 1;
                        float db = snap[colBase + b];
                        float t = DbToNorm(db, dbFloor, dbCeil);
                        if (t > rowMax[yInBand]) rowMax[yInBand] = t;
                    }

                    for (int yInBand = 0; yInBand < bandH; yInBand++)
                    {
                        float t = rowMax[yInBand];
                        if (t <= 0.01f) continue;
                        int q = Math.Min(levels - 1, (int)(t * levels));
                        g.FillRectangle(palette[q], x, s * bandH + yInBand, 1, 1);
                    }
                }
            }
        }
        finally
        {
            foreach (var brush in palette) brush.Dispose();
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

    // Scrolling loudness history (one sample per paint frame, ~30 fps)
    private const int LoudnessHistorySize = 600; // ~20 s at 30 fps
    private readonly float[] _histRms = new float[LoudnessHistorySize];
    private readonly float[] _histPeak = new float[LoudnessHistorySize];
    private readonly float[] _histTrue = new float[LoudnessHistorySize];
    private int _histPos;

    private void PushLoudnessSample(float rms, float peak, float truePk)
    {
        _histRms[_histPos] = rms;
        _histPeak[_histPos] = peak;
        _histTrue[_histPos] = truePk;
        _histPos = (_histPos + 1) % LoudnessHistorySize;
    }

    private void DrawLoudness(Graphics g, MeterService meters)
    {
        int w = ClientRectangle.Width, h = ClientRectangle.Height;
        var output = meters.Output;
        var tp = meters.TruePeak;
        PushLoudnessSample(output.RmsDb, output.HeldPeakDb, tp.HeldPeakDb);

        // Left ~72%: scrolling level history graph
        int graphW = (int)(w * 0.72);
        int graphX = 34, graphY = 8;
        int graphH = h - 30;

        using var gridPen = new Pen(GridColor);
        for (int db = -60; db <= 0; db += 12)
        {
            float y = graphY + graphH * (1f - DbToNorm(db, -60, 0));
            g.DrawLine(gridPen, graphX, y, graphX + graphW, y);
            using var f = new Font("Consolas", 7F);
            g.DrawString($"{db}", f, Brushes.Gray, 4, y - 5);
        }

        DrawHistoryLine(g, _histRms, graphX, graphY, graphW, graphH, Color.DodgerBlue, 1.4f, "RMS");
        DrawHistoryLine(g, _histPeak, graphX, graphY, graphW, graphH, AccentColor, 1.2f, "Peak");
        DrawHistoryLine(g, _histTrue, graphX, graphY, graphW, graphH, Color.OrangeRed, 1.0f, "TruePk");

        // Right side: instantaneous bars
        int barX = graphX + graphW + 16;
        int barW = Math.Min(28, (w - barX - 8) / 3 - 8);
        DrawLevelBar(g, barX, 10, barW, h - 30, output.RmsDb, -60, 0, Color.DodgerBlue, "RMS");
        DrawLevelBar(g, barX + barW + 12, 10, barW, h - 30, output.HeldPeakDb, -60, 0, AccentColor, "Peak");
        DrawLevelBar(g, barX + 2 * (barW + 12), 10, barW, h - 30, tp.HeldPeakDb, -60, 0, Color.OrangeRed, "TruePk");

        if (output.Clip || tp.Clip)
        {
            using var warn = new SolidBrush(Color.Red);
            g.DrawString("CLIP", new Font("Segoe UI", 12F, FontStyle.Bold), warn, graphX + graphW / 2 - 20, 4);
        }
    }

    private void DrawHistoryLine(Graphics g, float[] history, int x, int y, int w, int h, Color color, float penWidth, string label)
    {
        using var pen = new Pen(color, penWidth);
        int count = Math.Min(history.Length, LoudnessHistorySize);
        PointF? prev = null;
        for (int i = 0; i < count; i++)
        {
            // oldest -> newest left to right; _histPos is next write = oldest when full
            int idx = (_histPos + i) % history.Length;
            float db = history[idx];
            float px = x + (float)i / count * w;
            float py = y + h * (1f - DbToNorm(db, -60, 0));
            var pt = new PointF(px, py);
            if (prev.HasValue) g.DrawLine(pen, prev.Value, pt);
            prev = pt;
        }
        // label at right edge
        float lastDb = history[(_histPos + count - 1) % history.Length];
        g.DrawString($"{label} {lastDb:F1}", new Font("Consolas", 8F), new SolidBrush(color), x + 4, y + 4 + Array.IndexOf(new[] { _histRms, _histPeak, _histTrue }, history) * 14);
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
        string line1 = $"Out RMS {output.RmsDb:F1} dB  Peak {output.HeldPeakDb:F1} dB  TruePk {tp.HeldPeakDb:F1} dBTP";
        string line2 = $"Mode: {_mode}   Theme: {_theme}   FFT {meters.PostEq.FftSize} {meters.PostEq.Window}   {(_logFreq ? "Log" : "Lin")}   {(PeakHoldEnabled ? "Peak-hold" : "")}";

        // Translucent backdrop so traces never strike through the text
        var size1 = g.MeasureString(line1, font);
        var size2 = g.MeasureString(line2, font);
        float pad = 4f;
        var bgRect = new RectangleF(4, ClientRectangle.Height - size1.Height - size2.Height - pad * 4 - 6,
            Math.Max(size1.Width, size2.Width) + pad * 2, size1.Height + size2.Height + pad * 2);
        using (var bg = new SolidBrush(Color.FromArgb(170, 12, 12, 16)))
            g.FillRectangle(bg, bgRect);

        using var brush = new SolidBrush(Color.Gainsboro);
        float y = bgRect.Y + pad;
        g.DrawString(line1, font, brush, bgRect.X + pad, y);
        g.DrawString(line2, font, brush, bgRect.X + pad, y + size1.Height + pad);
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
