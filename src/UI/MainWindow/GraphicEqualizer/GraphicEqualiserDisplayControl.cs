using MusicForge.UI.Services;
using MusicForge.UI.Dialogs;
using HelixAudio.DSP;
using MusicForge.UI.Shared;
using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MusicForge.UI.MainWindow.GraphicEqualizer;

/// <summary>
/// Graphic equaliser display: global controls (enable/bypass/flat, in/out
/// gain, presets) on top, 31 ISO third-octave band sliders in the middle,
/// input/output peak+RMS meters with clip LEDs at the bottom.
/// Backed by <see cref="EqService"/> and <see cref="MeterService"/>.
/// </summary>
public partial class GraphicEqualiserDisplayControl : UserControl
{
    private static readonly Color BgDark = Color.FromArgb(24, 24, 26);
    private static readonly Color PanelDark = Color.FromArgb(38, 38, 42);
    private static readonly Color TrackColor = Color.FromArgb(70, 70, 74);
    private static readonly Color FillColor = Color.FromArgb(10, 71, 138);
    private static readonly Color TextColor = Color.FromArgb(212, 212, 212);
    private static readonly Color DimText = Color.FromArgb(140, 140, 150);

    private readonly ImageSlider[] _bandSliders = new ImageSlider[31];
    private readonly Label[] _bandValues = new Label[31];
    private readonly Label[] _bandFreqs = new Label[31];
    private readonly Panel?[] _bandColumns = new Panel?[31];
    private readonly Panel _curvePanel = new();
    private readonly Panel _spectrumPanel = new();
    private readonly Panel _sliderArea = new();
    private readonly Panel _meterPanel = new();
    private readonly System.Windows.Forms.Timer _meterTimer = new() { Interval = 100 };

    private CheckBox _enabledBox = null!;
    private Button _bypassButton = null!;
    private Button _flatButton = null!;
    private ImageSlider _inputGain = null!;
    private ImageSlider _outputGain = null!;
    private Label _inputGainLabel = null!;
    private Label _outputGainLabel = null!;
    private ComboBox _presetBox = null!;
    private Button _savePresetButton = null!;
    private Button _deletePresetButton = null!;
    private Label _channelLabel = null!;
    private Label _qLabel = null!;

    private bool _suppress;
    private int _editChannel = -1;   // -1 = linked/all channels, 0/1 = unlinked L/R
    private readonly EqService _eq = EqService.Instance;
    private readonly MeterService _meters = MeterService.Instance;

    public GraphicEqualiserDisplayControl()
    {
        InitializeComponent();
        BuildUi();
        _eq.Changed += (_, _) => RefreshFromService();
        _meterTimer.Tick += (_, _) => { _meterPanel.Invalidate(); _spectrumPanel.Invalidate(); _curvePanel.Invalidate(); };
        _meterTimer.Start();
        RefreshFromService();
        RefreshPresets();
    }

    // ---------- UI construction ----------

    private void BuildUi()
    {
        BackColor = BgDark;
        Dock = DockStyle.Fill;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = BgDark,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // global controls
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));  // EQ response curve
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // band sliders
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));  // pre/post spectrum
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));   // meters

        root.Controls.Add(BuildControlStrip(), 0, 0);
        root.Controls.Add(_curvePanel, 0, 1);
        root.Controls.Add(_sliderArea, 0, 2);
        root.Controls.Add(_spectrumPanel, 0, 3);
        root.Controls.Add(_meterPanel, 0, 4);
        Controls.Add(root);

        BuildSliders();

        _curvePanel.Dock = DockStyle.Fill;
        _curvePanel.BackColor = BgDark;
        _curvePanel.Paint += CurvePanel_Paint;
        _spectrumPanel.Dock = DockStyle.Fill;
        _spectrumPanel.BackColor = BgDark;
        _spectrumPanel.Paint += SpectrumPanel_Paint;
        _meterPanel.Dock = DockStyle.Fill;
        _meterPanel.Paint += MeterPanel_Paint;
    }

    private Control BuildControlStrip()
    {
        var strip = new Panel { Dock = DockStyle.Fill, BackColor = PanelDark };

        _enabledBox = new CheckBox
        {
            Text = "EQ",
            Checked = _eq.Enabled,
            AutoSize = true,
            ForeColor = TextColor,
            Location = new Point(10, 11)
        };
        _enabledBox.CheckedChanged += (_, _) => _eq.Enabled = _enabledBox.Checked;

        _bypassButton = new Button
        {
            Text = _eq.Bypass ? "Bypassed" : "Bypass",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = _eq.Bypass ? Color.FromArgb(140, 60, 60) : Color.FromArgb(70, 70, 74),
            ForeColor = Color.White,
            Location = new Point(60, 8)
        };
        _bypassButton.Click += (_, _) => { _eq.Bypass = !_eq.Bypass; };

        _flatButton = new Button
        {
            Text = "Flat",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 70, 74),
            ForeColor = Color.White,
            Location = new Point(130, 8)
        };
        _flatButton.Click += (_, _) => _eq.Flat();

        // Input gain trim (-12..+12 dB in 0.1 dB units)
        _inputGain = MakeGainSlider();
        _inputGainLabel = MakeGainLabel();
        _inputGain.ValueChanged += (_, _) => { _inputGainLabel.Text = FmtDb(_inputGain.Value / 10f); _eq.InputGainDb = _inputGain.Value / 10f; };

        _outputGain = MakeGainSlider();
        _outputGainLabel = MakeGainLabel();
        _outputGain.ValueChanged += (_, _) => { _outputGainLabel.Text = FmtDb(_outputGain.Value / 10f); _eq.OutputGainDb = _outputGain.Value / 10f; };

        _presetBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 160,
            BackColor = PanelDark,
            ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(320, 9)
        };
        _presetBox.SelectedIndexChanged += PresetBox_SelectedIndexChanged;

        _savePresetButton = new Button
        {
            Text = "Save",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 70, 74),
            ForeColor = Color.White,
            Location = new Point(486, 8)
        };
        _savePresetButton.Click += (_, _) => SavePreset();

        _deletePresetButton = new Button
        {
            Text = "Delete",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(140, 60, 60),
            ForeColor = Color.White,
            Location = new Point(550, 8)
        };
        _deletePresetButton.Click += (_, _) => DeletePreset();

        // Layout helpers
        var inLabel = new Label { Text = "In", ForeColor = DimText, AutoSize = true, Location = new Point(690, 11) };
        _inputGain.Location = new Point(710, 8);
        _inputGainLabel.Location = new Point(832, 11);
        var outLabel = new Label { Text = "Out", ForeColor = DimText, AutoSize = true, Location = new Point(880, 11) };
        _outputGain.Location = new Point(900, 8);
        _outputGainLabel.Location = new Point(1032, 11);

        _channelLabel = new Label { Text = "CH: LINKED", ForeColor = DimText, AutoSize = true, Location = new Point(1100, 11), Font = new Font("Segoe UI", 7F, FontStyle.Bold) };
        _qLabel = new Label { Text = "", ForeColor = DimText, AutoSize = true, Location = new Point(1180, 11), Font = new Font("Segoe UI", 7F) };

        foreach (var c in new Control[] { _enabledBox, _bypassButton, _flatButton, inLabel, _inputGain, _inputGainLabel, outLabel, _outputGain, _outputGainLabel, _presetBox, _savePresetButton, _deletePresetButton, _channelLabel, _qLabel })
            strip.Controls.Add(c);

        return strip;
    }

    private static ImageSlider MakeGainSlider() => new()
    {
        Width = 120,
        Height = 36,
        Orientation = Orientation.Horizontal,
        Minimum = -120,
        Maximum = 120,
        WheelStep = 1
    };

    private static Label MakeGainLabel() => new()
    {
        Width = 50,
        ForeColor = TextColor,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private void BuildSliders()
    {
        _sliderArea.Dock = DockStyle.Fill;
        _sliderArea.BackColor = BgDark;
        _sliderArea.Padding = new Padding(8, 4, 8, 4);

        // 31 equal columns that always fill the available width - no scrollbar
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 31,
            RowCount = 1,
            BackColor = BgDark,
            Padding = new Padding(0)
        };
        for (int i = 0; i < 31; i++)
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 31f));

        for (int i = 0; i < 31; i++)
        {
            int band = i;
            var col = new Panel { Dock = DockStyle.Fill, BackColor = BgDark, Margin = new Padding(1, 0, 1, 0) };

            var freq = new Label
            {
                Text = FmtFreq(_eq.Frequencies[i]),
                ForeColor = DimText,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Segoe UI", 7F)
            };
            _bandFreqs[i] = freq;

            var slider = new ImageSlider
            {
                Orientation = Orientation.Vertical,
                Dock = DockStyle.Fill,
                Minimum = -150,
                Maximum = 150,
                Value = 0,
                WheelStep = 5    // 0.5 dB per notch
            };
            slider.ValueChanged += (_, _) => OnBandSlider(band, slider);
            _bandSliders[i] = slider;

            var val = new Label
            {
                Text = "+0.0",
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                Height = 18,
                Font = new Font("Segoe UI", 7F)
            };
            _bandValues[i] = val;
            _bandColumns[i] = col;

            int bandIndex = i;
            var bandMenu = new ContextMenuStrip();
            var toggleItem = new ToolStripMenuItem("Disable Band");
            toggleItem.Click += (_, _) =>
            {
                var enabled = _eq.Node.GetBandEnabled();
                _eq.SetBandEnabled(bandIndex, !enabled[bandIndex]);
            };
            bandMenu.Opening += (_, _) =>
            {
                var enabled = _eq.Node.GetBandEnabled();
                toggleItem.Text = enabled[bandIndex] ? "Disable Band" : "Enable Band";
            };
            bandMenu.Items.Add(toggleItem);
            bandMenu.Items.Add(new ToolStripSeparator());
            var zeroItem = new ToolStripMenuItem("Set to 0.0 dB", null, (_, _) =>
            {
                _bandSliders[bandIndex].Value = 0;
            });
            bandMenu.Items.Add(zeroItem);
            col.ContextMenuStrip = bandMenu;

            val.DoubleClick += (_, _) =>
            {
                string? text = InputDialog.Show(this, $"Band {FmtFreq(_eq.Frequencies[bandIndex])} Hz - gain in dB (-15 to +15):", "Band Gain");
                if (float.TryParse(text, out float db))
                    _bandSliders[bandIndex].Value = (int)Math.Round(Math.Clamp(db, -15f, 15f) * 10);
            };

            col.Controls.Add(slider);
            col.Controls.Add(freq);
            col.Controls.Add(val);
            content.Controls.Add(col);
        }

        _sliderArea.Controls.Add(content);
    }

    // ---------- Band slider handling ----------

    private void OnBandSlider(int band, ImageSlider slider)
    {
        if (_suppress) return;
        float db = slider.Value / 10f;
        _bandValues[band].Text = FmtDb(db);
        _eq.SetBandGain(_editChannel, band, db);
    }

    private void RefreshFromService()
    {
        if (InvokeRequired) { BeginInvoke(new Action(RefreshFromService)); return; }
        _suppress = true;
        try
        {
            _enabledBox.Checked = _eq.Enabled;
            _bypassButton.Text = _eq.Bypass ? "Bypassed" : "Bypass";
            _bypassButton.BackColor = _eq.Bypass ? Color.FromArgb(140, 60, 60) : Color.FromArgb(70, 70, 74);
            _inputGain.Value = (int)Math.Round(_eq.InputGainDb * 10);
            _outputGain.Value = (int)Math.Round(_eq.OutputGainDb * 10);
            _inputGainLabel.Text = FmtDb(_eq.InputGainDb);
            _outputGainLabel.Text = FmtDb(_eq.OutputGainDb);

            var gains = _editChannel >= 0 ? _eq.GetBandGains(_editChannel) : _eq.BandGains;
            var enabled = _eq.Node.GetBandEnabled();
            for (int i = 0; i < 31; i++)
            {
                _bandSliders[i].Value = (int)Math.Round(gains[i] * 10);
                _bandValues[i].Text = FmtDb(gains[i]);
                bool on = enabled[i];
                if (_bandColumns[i] is { } col)
                {
                    col.BackColor = on ? BgDark : Color.FromArgb(40, 40, 44);
                    _bandFreqs[i].ForeColor = on ? DimText : Color.FromArgb(90, 90, 96);
                    _bandValues[i].ForeColor = on ? TextColor : Color.FromArgb(90, 90, 96);
                    _bandSliders[i].Enabled = on;
                }
            }
            _channelLabel.Text = _editChannel < 0 ? "CH: LINKED" : $"CH: {(_editChannel == 0 ? "LEFT" : "RIGHT")}";
            _channelLabel.ForeColor = _editChannel < 0 ? DimText : Color.FromArgb(220, 180, 60);
            _qLabel.Text = $"Q {_eq.BandQ:0.##}{(_eq.Mode == MusicForge.UI.Services.EqFilterMode.LinearPhase ? " LP" : "")}";
        }
        finally { _suppress = false; }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    /// <summary>Which channel the sliders edit when channels are unlinked
    /// (-1 = linked/all, 0 = left, 1 = right).</summary>
    public int EditChannel
    {
        get => _editChannel;
        set { _editChannel = value; RefreshFromService(); }
    }

    /// <summary>Exports the EQ response curve as PNG (drawn panel) or JSON (points).</summary>
    public void ExportResponseCurve()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png|JSON curve (*.json)|*.json",
            FileName = "eq-response"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            if (dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var points = _eq.Node.GetMagnitudeResponseDb();
                System.IO.File.WriteAllText(dlg.FileName, System.Text.Json.JsonSerializer.Serialize(
                    new { Frequencies = _eq.Frequencies, ResponseDb = points }));
            }
            else
            {
                using var bmp = new Bitmap(Math.Max(400, _curvePanel.Width), Math.Max(120, _curvePanel.Height));
                _curvePanel.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Export failed:\n{ex.Message}", "EQ Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---------- Presets ----------

    private void RefreshPresets()
    {
        _presetBox.Items.Clear();
        _presetBox.Items.Add("(select preset)");
        foreach (var (name, _) in _eq.FactoryPresets()) _presetBox.Items.Add("Factory: " + name);
        foreach (var p in _eq.UserPresets()) _presetBox.Items.Add("User: " + p.Name + $"#{p.Id}");
        _presetBox.SelectedIndex = 0;
    }

    private void PresetBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppress || _presetBox.SelectedIndex <= 0) return;
        string sel = _presetBox.SelectedItem?.ToString() ?? "";
        if (sel.StartsWith("Factory: ", StringComparison.Ordinal))
        {
            string name = sel["Factory: ".Length..];
            var preset = _eq.FactoryPresets().FirstOrDefault(p => p.Name == name);
            if (preset.Gains is not null) _eq.SetAllGains(preset.Gains);
        }
        else if (sel.StartsWith("User: ", StringComparison.Ordinal))
        {
            int hash = sel.LastIndexOf('#');
            if (hash > 0 && int.TryParse(sel[(hash + 1)..], out int id))
            {
                var preset = _eq.UserPresets().FirstOrDefault(p => p.Id == id);
                if (preset is not null) _eq.LoadPreset(preset);
            }
        }
        _presetBox.SelectedIndex = 0;
    }

    private void SavePreset()
    {
        string? name = InputDialog.Show(this, "Enter preset name:", "Save EQ Preset");
        if (string.IsNullOrWhiteSpace(name)) return;
        _eq.SavePreset(name.Trim());
        RefreshPresets();
    }

    private void DeletePreset()
    {
        string sel = _presetBox.SelectedItem?.ToString() ?? "";
        if (!sel.StartsWith("User: ", StringComparison.Ordinal)) return;
        int hash = sel.LastIndexOf('#');
        if (hash <= 0 || !int.TryParse(sel[(hash + 1)..], out int id)) return;
        _eq.DeletePreset(id);
        RefreshPresets();
    }

    // ---------- Meters ----------

    private void MeterPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (ShowInputMeter)
            DrawMeter(e.Graphics, new Rectangle(10, 8, Width - 20, 30), "INPUT", _meters.Input);
        if (ShowOutputMeter)
            DrawMeter(e.Graphics, new Rectangle(10, 48, Width - 20, 30), "OUTPUT", _meters.Output, showTruePeak: true);
    }

    private void DrawMeter(Graphics g, Rectangle rect, string label, LevelMeterNode meter, bool showTruePeak = false)
    {
        // Label
        TextRenderer.DrawText(g, label, Font, new Rectangle(rect.X, rect.Y, 60, rect.Height),
            DimText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        var barRect = new Rectangle(rect.X + 65, rect.Y + 4, rect.Width - 75, rect.Height - 8);
        // dB range -60..0
        float DbToX(float db)
        {
            float t = Math.Clamp((db + 60f) / 60f, 0f, 1f);
            return barRect.X + t * barRect.Width;
        }

        using (var bg = new SolidBrush(TrackColor))
            g.FillRectangle(bg, barRect);

        // Headroom markers at -18/-12/-6/0 dBFS
        using (var markerPen = new Pen(Color.FromArgb(70, 70, 76)))
        using (var markerFont = new Font("Segoe UI", 6F))
        {
            foreach (var m in new[] { -18f, -12f, -6f, 0f })
            {
                float mx = DbToX(m);
                g.DrawLine(markerPen, mx, barRect.Y, mx, barRect.Bottom);
                TextRenderer.DrawText(g, m == 0f ? "0" : m.ToString("0"), markerFont,
                    new Rectangle((int)mx - 8, barRect.Bottom + 1, 16, 10), DimText,
                    TextFormatFlags.Top | TextFormatFlags.HorizontalCenter);
            }
        }

        // True-peak marker (oversampled estimate)
        if (showTruePeak)
        {
            float tpX = DbToX(_meters.TruePeak.HeldPeakDb);
            using (var tpPen = new Pen(Color.FromArgb(230, 140, 60), 2f))
                g.DrawLine(tpPen, tpX, barRect.Y - 2, tpX, barRect.Bottom + 2);
            TextRenderer.DrawText(g, "TP", new Font("Segoe UI", 6F, FontStyle.Bold),
                new Rectangle(barRect.Right + 2, barRect.Y - 4, 20, 10), Color.FromArgb(230, 140, 60));
        }

        // Held peak marker
        if (ShowPeakHold)
        {
        float heldX = DbToX(meter.HeldPeakDb);
        using (var peakBrush = new SolidBrush(Color.FromArgb(220, 180, 60)))
            g.FillRectangle(peakBrush, heldX - 1, barRect.Y, 2, barRect.Height);
        }

        // RMS bar
        float rmsX = DbToX(meter.RmsDb);
        using (var rmsBrush = new SolidBrush(Color.FromArgb(80, 160, 220)))
            g.FillRectangle(rmsBrush, barRect.X, barRect.Y, Math.Max(0, rmsX - barRect.X), barRect.Height);

        // Peak bar
        float peakX = DbToX(meter.PeakDb);
        using (var peakBar = new SolidBrush(Color.FromArgb(120, 200, 120)))
            g.FillRectangle(peakBar, barRect.X, barRect.Y, Math.Max(0, peakX - barRect.X), barRect.Height / 2);

        // Clip LED at right
        if (!ShowClip) return;
        var led = new Rectangle(barRect.Right + 6, barRect.Y + 2, 14, barRect.Height - 4);
        using (var ledBrush = new SolidBrush(meter.Clip ? Color.FromArgb(220, 60, 60) : Color.FromArgb(60, 60, 64)))
            g.FillRectangle(ledBrush, led);
        TextRenderer.DrawText(g, "CLIP", Font, new Rectangle(led.Right + 2, led.Y, 40, led.Height),
            meter.Clip ? Color.FromArgb(220, 60, 60) : DimText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    // ---------- Public band-mode / meter API (used by the ribbon) ----------

    /// <summary>Shows only the given band indices and disables the rest in the
    /// engine (their gains are preserved). Pass all 31 indices for full mode.</summary>
    public void SetBandMode(IReadOnlyCollection<int> visibleBands)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => SetBandMode(visibleBands))); return; }
        for (int i = 0; i < 31; i++)
        {
            bool visible = visibleBands.Contains(i);
            if (_bandColumns[i] is { } col) col.Visible = visible;
            _eq.SetBandEnabled(i, visible);
        }
    }

    /// <summary>Meter display toggles driven by the ribbon Levels/Display menus.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowInputMeter { get; set; } = true;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowOutputMeter { get; set; } = true;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowPeakHold { get; set; } = true;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowClip { get; set; } = true;

    /// <summary>Current band gains snapshot (for A/B compare).</summary>
    public (float[] Gains, float InDb, float OutDb, bool Bypass, bool Enabled) Snapshot()
        => (_eq.BandGains, _eq.InputGainDb, _eq.OutputGainDb, _eq.Bypass, _eq.Enabled);

    public void Restore((float[] Gains, float InDb, float OutDb, bool Bypass, bool Enabled) s)
    {
        _eq.SetAllGains(s.Gains);
        _eq.InputGainDb = s.InDb;
        _eq.OutputGainDb = s.OutDb;
        _eq.Bypass = s.Bypass;
        _eq.Enabled = s.Enabled;
    }
    // ---------- Helpers ----------

    private static string FmtFreq(float hz) => hz switch
    {
        >= 1000 => $"{hz / 1000f:0.##}k".Replace(".5k", ".5k"),
        _ => hz.ToString("0.#")
    };

    private static string FmtDb(float db) => $"{db:+0.0;-0.0;+0.0}";

    // ---------- Response curve ----------

    private const float CurveMinDb = -15f, CurveMaxDb = 15f;
    private const float CurveMinHz = 20f, CurveMaxHz = 20000f;

    private static float LogFreqToX(float hz, Rectangle r)
    {
        float t = (MathF.Log10(Math.Clamp(hz, CurveMinHz, CurveMaxHz)) - MathF.Log10(CurveMinHz))
                / (MathF.Log10(CurveMaxHz) - MathF.Log10(CurveMinHz));
        return r.X + t * r.Width;
    }

    private static float DbToY(float db, Rectangle r)
    {
        float t = Math.Clamp((db - CurveMinDb) / (CurveMaxDb - CurveMinDb), 0f, 1f);
        return r.Bottom - t * r.Height;
    }

    private void CurvePanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        TextRenderer.DrawText(g, "EQ RESPONSE - dB", new Font("Segoe UI", 8F, FontStyle.Bold), new Rectangle(10, 4, 200, 16), DimText);
        var rect = new Rectangle(8, 24, _curvePanel.Width - 16, _curvePanel.Height - 30);

        using (var bg = new SolidBrush(PanelDark)) g.FillRectangle(bg, rect);
        using (var border = new Pen(Color.FromArgb(60, 60, 64))) g.DrawRectangle(border, rect);

        // dB gridlines + labels
        using var gridPen = new Pen(Color.FromArgb(55, 55, 60));
        using var zeroPen = new Pen(Color.FromArgb(90, 90, 96));
        using var labelFont = new Font("Segoe UI", 7F);
        foreach (var db in new[] { -12f, -6f, 0f, 6f, 12f })
        {
            float y = DbToY(db, rect);
            g.DrawLine(db == 0f ? zeroPen : gridPen, rect.X, y, rect.Right, y);
            TextRenderer.DrawText(g, $"{db:+0;-0;0}", labelFont,
                new Rectangle(rect.X + 2, (int)y - 7, 30, 14), DimText,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        // Frequency gridlines (decade-ish marks)
        foreach (var hz in new[] { 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f })
        {
            float x = LogFreqToX(hz, rect);
            g.DrawLine(gridPen, x, rect.Y, x, rect.Bottom);
            string lbl = hz >= 1000 ? $"{hz / 1000f:0.#}k" : hz.ToString("0");
            TextRenderer.DrawText(g, lbl, labelFont,
                new Rectangle((int)x - 15, rect.Bottom - 12, 30, 12), DimText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }

        // Response curve
        var response = _eq.Node.GetMagnitudeResponseDb(220, CurveMinHz, CurveMaxHz);
        if (response.Length > 1)
        {
            var pts = new Point[response.Length];
            for (int i = 0; i < response.Length; i++)
            {
                float t = (float)i / (response.Length - 1);
                float hz = CurveMinHz * MathF.Pow(CurveMaxHz / CurveMinHz, t);
                pts[i] = new Point((int)LogFreqToX(hz, rect), (int)DbToY(response[i], rect));
            }
            using var curvePen = new Pen(Color.FromArgb(240, 190, 80), 2f);
            g.DrawLines(curvePen, pts);

            // Fill under curve to 0 dB line
            var fill = new Point[response.Length + 2];
            Array.Copy(pts, fill, pts.Length);
            float zeroY = DbToY(0f, rect);
            fill[pts.Length] = new Point(pts[^1].X, (int)zeroY);
            fill[pts.Length + 1] = new Point(pts[0].X, (int)zeroY);
            using var fillBrush = new SolidBrush(Color.FromArgb(28, 240, 190, 80));
            g.FillPolygon(fillBrush, fill);
        }

        if (!_eq.Enabled)
        {
            TextRenderer.DrawText(g, "EQ DISABLED", Font, rect, DimText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // ---------- Pre/post spectrum ----------

    private const float SpecMinDb = -90f, SpecMaxDb = 0f;

    private static float SpecDbToY(float db, Rectangle r)
    {
        float t = Math.Clamp((db - SpecMinDb) / (SpecMaxDb - SpecMinDb), 0f, 1f);
        return r.Bottom - t * r.Height;
    }

    private void SpectrumPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        TextRenderer.DrawText(g, "SPECTRUM - PRE (blue) vs POST-EQ (green)", new Font("Segoe UI", 8F, FontStyle.Bold), new Rectangle(10, 4, 320, 16), DimText);
        var rect = new Rectangle(8, 24, _spectrumPanel.Width - 16, _spectrumPanel.Height - 30);

        using (var bg = new SolidBrush(PanelDark)) g.FillRectangle(bg, rect);
        using (var border = new Pen(Color.FromArgb(60, 60, 64))) g.DrawRectangle(border, rect);

        using var gridPen = new Pen(Color.FromArgb(55, 55, 60));
        using var labelFont = new Font("Segoe UI", 7F);
        foreach (var db in new[] { -60f, -30f })
        {
            float y = SpecDbToY(db, rect);
            g.DrawLine(gridPen, rect.X, y, rect.Right, y);
            TextRenderer.DrawText(g, $"{db:0}", labelFont,
                new Rectangle(rect.X + 2, (int)y - 7, 30, 14), DimText,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
        foreach (var hz in new[] { 100f, 1000f, 10000f })
        {
            float x = LogFreqToX(hz, rect);
            g.DrawLine(gridPen, x, rect.Y, x, rect.Bottom);
            string lbl = hz >= 1000 ? $"{hz / 1000f:0.#}k" : hz.ToString("0");
            TextRenderer.DrawText(g, lbl, labelFont,
                new Rectangle((int)x - 15, rect.Bottom - 12, 30, 12), DimText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }

        DrawSpectrum(g, rect, _meters.PreEq, Color.FromArgb(110, 140, 170), "PRE");
        DrawSpectrum(g, rect, _meters.PostEq, Color.FromArgb(120, 210, 130), "POST");

        // Legend
        TextRenderer.DrawText(g, "PRE", labelFont, new Rectangle(rect.Right - 60, rect.Y + 2, 28, 12),
            Color.FromArgb(110, 140, 170));
        TextRenderer.DrawText(g, "POST", labelFont, new Rectangle(rect.Right - 30, rect.Y + 2, 30, 12),
            Color.FromArgb(120, 210, 130));
    }

    private void DrawSpectrum(Graphics g, Rectangle rect, SpectrumTapNode tap, Color color, string label)
    {
        if (!tap.HasData) return;
        var spec = tap.SpectrumDb;
        float binHz = tap.BinWidthHz;
        if (binHz <= 0) return;

        int cols = rect.Width;
        var pts = new Point[cols];
        for (int x = 0; x < cols; x++)
        {
            float t = (float)x / cols;
            float hz = CurveMinHz * MathF.Pow(CurveMaxHz / CurveMinHz, t);
            int bin = Math.Clamp((int)(hz / binHz), 0, spec.Length - 1);
            pts[x] = new Point(rect.X + x, (int)SpecDbToY(spec[bin], rect));
        }
        using var pen = new Pen(color, label == "POST" ? 1.6f : 1.2f);
        g.DrawLines(pen, pts);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _meterTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
