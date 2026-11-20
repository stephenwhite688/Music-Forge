using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using AudioForge.UI.Dialogs;
using AudioForge.UI.MainWindow.GraphicEqualizer;
using AudioForge.UI.Services;
using HelixAudio.DSP.GraphicEQ;

namespace AudioForge.UI.MainWindow.Ribbons;

public partial class GraphicEqualiserRibbonControl : RibbonStripControl
{
    private readonly GraphicEqualiserDisplayControl _display = new();
    private readonly EqService _eq = EqService.Instance;

    // ISO 31-band indices for the reduced band modes
    private static readonly int[] Band10 = { 2, 5, 8, 11, 14, 17, 20, 23, 26, 29 };  // 31.5 - 16k
    private static readonly int[] Band15 = { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29 };
    private static readonly int[] Band31 = Enumerable.Range(0, 31).ToArray();

    // A/B compare snapshot
    private (float[] Gains, float InDb, float OutDb, bool Bypass, bool Enabled)? _abSnapshot;

    public GraphicEqualiserRibbonControl()
    {
        InitializeComponent();
        BuildRibbon();
    }

    public override Control? TabContent => _display;

    private void BuildRibbon()
    {
        var groups = new[]
        {
            BuildTargetGroup(),
            BuildEqControlGroup(),
            BuildBandModeGroup(),
            BuildDisplayGroup(),
            BuildLevelsGroup(),
            BuildPresetsGroup(),
            BuildEngineGroup()
        };

        // WinForms docks later-added controls closer to the edge, so add in
        // reverse order to keep Target leftmost and Engine rightmost.
        foreach (var group in groups.Reverse())
        {
            group.Dock = DockStyle.Left;
            group.Caption = string.Empty; // single ribbon-wide caption below
            RibbonPanel.Controls.Add(group);
        }

        RibbonPanel.Controls.Add(new Label
        {
            Text = "Graphic Equalizer",
            Dock = DockStyle.Bottom,
            Height = 13,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(51, 51, 55),
            Font = new Font("Segoe UI", 7.5F)
        });
    }

    private static ContextMenuStrip BuildMenu() => new();

    private static void AddItem(ContextMenuStrip menu, string text, Action handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) => handler();
        menu.Items.Add(item);
    }

    private static void AddItem(ToolStripMenuItem parent, string text, Action handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (s, e) => handler();
        parent.DropDownItems.Add(item);
    }

    private static void AddSeparator(ContextMenuStrip menu) => menu.Items.Add(new ToolStripSeparator());

    private static void Info(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    // ---------- Target ----------

    private RibbonGroupPanel BuildTargetGroup()
    {
        var menu = BuildMenu();
        AddItem(menu, "EQ Status", () =>
        {
            var sr = _eq.Node.Format?.SampleRate ?? 0;
            var ch = _eq.Node.Format?.Channels ?? 0;
            int active = _eq.BandGains.Count(g => MathF.Abs(g) > 0.05f);
            Info("EQ Status",
                $"Enabled:   {(_eq.Enabled ? "yes" : "no")}\n" +
                $"Bypassed:  {(_eq.Bypass ? "yes" : "no")}\n" +
                $"Bands with gain: {active} / {_eq.BandCount}\n" +
                $"Input trim:  {_eq.InputGainDb:+0.0;-0.0} dB\n" +
                $"Output trim: {_eq.OutputGainDb:+0.0;-0.0} dB\n" +
                $"Stream format: {(sr > 0 ? $"{sr} Hz, {ch} ch" : "not playing")}");
        });
        AddItem(menu, "Signal Path", () =>
            Info("Signal Path",
                "Input meter -> 31-band EQ -> output meter -> WASAPI output.\n\n" +
                "The same chain processes local files and internet radio.\n" +
                "Bands above ~45% of the sample rate are transparent\n" +
                "(stability guard near Nyquist)."));

        var group = new RibbonGroupPanel("Target") { ContextMenuStrip = menu };
        group.AddButton("Target", menu);
        return group;
    }

    // ---------- EQ Control ----------

    private RibbonGroupPanel BuildEqControlGroup()
    {
        var menu = BuildMenu();
        AddItem(menu, "Enable EQ", () => _eq.Enabled = true);
        AddItem(menu, "Disable EQ", () => _eq.Enabled = false);
        AddItem(menu, "Bypass", () => _eq.Bypass = !_eq.Bypass);
        AddItem(menu, "Flat Response", () => _eq.Flat());
        AddSeparator(menu);
        AddItem(menu, "A/B Compare", ToggleAB);
        AddItem(menu, "Undo", () => _eq.Undo());
        var modeMenu = new ToolStripMenuItem("Filter Mode");
        var minItem = new ToolStripMenuItem("Minimum Phase (low latency)");
        var linItem = new ToolStripMenuItem("Linear Phase (constant delay, ~85 ms)");
        minItem.Click += (_, _) => _eq.Mode = AudioForge.UI.Services.EqFilterMode.MinimumPhase;
        linItem.Click += (_, _) => _eq.Mode = AudioForge.UI.Services.EqFilterMode.LinearPhase;
        menu.Opening += (_, _) => { minItem.Checked = _eq.Mode == AudioForge.UI.Services.EqFilterMode.MinimumPhase; linItem.Checked = _eq.Mode == AudioForge.UI.Services.EqFilterMode.LinearPhase; };
        modeMenu.DropDownItems.Add(minItem);
        modeMenu.DropDownItems.Add(linItem);
        menu.Items.Add(modeMenu);

        var group = new RibbonGroupPanel("EQ Control") { ContextMenuStrip = menu };
        group.AddButton("EQ", menu);
        return group;
    }

    private void ToggleAB()
    {
        if (_abSnapshot is null)
        {
            _abSnapshot = _display.Snapshot();
            Info("A/B Compare", "Snapshot A stored. Adjust the EQ, then run A/B Compare again to switch back.");
        }
        else
        {
            _display.Restore(_abSnapshot.Value);
            _abSnapshot = null;
        }
    }

    // ---------- Band Mode ----------

    private RibbonGroupPanel BuildBandModeGroup()
    {
        var menu = BuildMenu();
        AddItem(menu, "10 Band", () => _display.SetBandMode(Band10));
        AddItem(menu, "15 Band", () => _display.SetBandMode(Band15));
        AddItem(menu, "31 Band (Full)", () => _display.SetBandMode(Band31));

        var group = new RibbonGroupPanel("Band Mode") { ContextMenuStrip = menu };
        group.AddButton("Bands", menu);
        return group;
    }

    // ---------- Display ----------

    private RibbonGroupPanel BuildChannelsGroup()
    {
        var menu = BuildMenu();
        AddItem(menu, "Link All Channels", () => { _eq.ChannelLinked = true; _display.EditChannel = -1; });
        AddItem(menu, "Edit Left Channel (Unlinked)", () => { _eq.ChannelLinked = false; _display.EditChannel = 0; });
        AddItem(menu, "Edit Right Channel (Unlinked)", () => { _eq.ChannelLinked = false; _display.EditChannel = 1; });

        var group = new RibbonGroupPanel("Channels") { ContextMenuStrip = menu };
        group.AddButton("Channels", menu);
        return group;
    }

    private RibbonGroupPanel BuildDisplayGroup()
    {
        var menu = BuildMenu();
        AddToggle(menu, "Show Input Meter", () => _display.ShowInputMeter, v => _display.ShowInputMeter = v);
        AddToggle(menu, "Show Output Meter", () => _display.ShowOutputMeter, v => _display.ShowOutputMeter = v);
        AddToggle(menu, "Show Peak Hold", () => _display.ShowPeakHold, v => _display.ShowPeakHold = v);
        AddToggle(menu, "Show Clip Indicator", () => _display.ShowClip, v => _display.ShowClip = v);

        var group = new RibbonGroupPanel("Display") { ContextMenuStrip = menu };
        group.AddButton("Display", menu);
        return group;
    }

    private void AddToggle(ContextMenuStrip menu, string text, Func<bool> get, Action<bool> set)
    {
        var item = new ToolStripMenuItem(text) { CheckOnClick = false };
        void Refresh() => item.Checked = get();
        item.Click += (s, e) => { set(!get()); Refresh(); _display.Invalidate(); };
        menu.Opening += (s, e) => Refresh();
        menu.Items.Add(item);
    }

    // ---------- Levels ----------

    private RibbonGroupPanel BuildLevelsGroup()
    {
        var menu = BuildMenu();
        AddItem(menu, "Input Gain…", () => SetTrim(true));
        AddItem(menu, "Output Gain…", () => SetTrim(false));
        AddItem(menu, "Reset Levels", () => { _eq.InputGainDb = 0; _eq.OutputGainDb = 0; });
        AddItem(menu, "Band Q (0.5-12)...", () =>
        {
            string? t = InputDialog.Show(_display.FindForm(), "Band Q factor (0.5 surgical .. 12 wide):", "EQ Band Q", _eq.BandQ.ToString("0.##"));
            if (float.TryParse(t, out float q)) _eq.BandQ = Math.Clamp(q, 0.5f, 12f);
        });

        var group = new RibbonGroupPanel("Levels") { ContextMenuStrip = menu };
        group.AddButton("Levels", menu);
        return group;
    }

    private void SetTrim(bool input)
    {
        string label = input ? "Input" : "Output";
        string? text = InputDialog.Show(_display.FindForm(), $"{label} gain in dB (-24 to +24):", "EQ Level");
        if (!float.TryParse(text, out float db)) return;
        db = Math.Clamp(db, -24f, 24f);
        if (input) _eq.InputGainDb = db; else _eq.OutputGainDb = db;
    }

    // ---------- Presets ----------

    private RibbonGroupPanel BuildPresetsGroup()
    {
        var menu = BuildMenu();

        var factoryMenu = new ToolStripMenuItem("Factory Presets");
        foreach (var (name, _) in _eq.FactoryPresets())
        {
            var presetName = name;
            factoryMenu.DropDownItems.Add(new ToolStripMenuItem(name, null, (s, e) => LoadFactory(presetName)));
        }
        menu.Items.Add(factoryMenu);

        var userMenu = new ToolStripMenuItem("User Presets");
        AddItem(userMenu, "Save Current as…", SaveUserPreset);
        userMenu.DropDownItems.Add(new ToolStripSeparator());
        userMenu.DropDownOpening += (_, _) => PopulateUserPresetItems(userMenu);
        menu.Items.Add(userMenu);

        var ioMenu = new ToolStripMenuItem("Import / Export");
        AddItem(ioMenu, "Export Current EQ…", ExportCurrent);
        AddItem(ioMenu, "Import EQ…", ImportEq);
        menu.Items.Add(ioMenu);

        var group = new RibbonGroupPanel("Presets") { ContextMenuStrip = menu };
        group.AddButton("Presets", menu);
        return group;
    }

    private void LoadFactory(string name)
    {
        var preset = _eq.FactoryPresets().FirstOrDefault(p => p.Name == name);
        if (preset.Gains is not null) _eq.SetAllGains(preset.Gains);
    }

    private void PopulateUserPresetItems(ToolStripMenuItem userMenu)
    {
        // Remove items added by previous openings (keep header + separator)
        while (userMenu.DropDownItems.Count > 2)
            userMenu.DropDownItems.RemoveAt(userMenu.DropDownItems.Count - 1);

        foreach (var p in _eq.UserPresets())
        {
            var load = new ToolStripMenuItem($"Load — {p.Name}", null, (s, e) => _eq.LoadPreset(p));
            userMenu.DropDownItems.Add(load);
        }

        if (_eq.UserPresets().Count > 0)
        {
            userMenu.DropDownItems.Add(new ToolStripSeparator());
            var rename = new ToolStripMenuItem("Rename…");
            var del = new ToolStripMenuItem("Delete…");
            foreach (var p in _eq.UserPresets())
            {
                var rec = p;
                var r = new ToolStripMenuItem($"Rename — {rec.Name}", null, (s, e) => RenamePreset(rec));
                rename.DropDownItems.Add(r);
                var d = new ToolStripMenuItem($"Delete — {rec.Name}", null, (s, e) => _eq.DeletePreset(rec.Id));
                del.DropDownItems.Add(d);
            }
            userMenu.DropDownItems.Add(rename);
            userMenu.DropDownItems.Add(del);
        }
    }

    private void SaveUserPreset()
    {
        string? name = InputDialog.Show(_display.FindForm(), "Enter preset name:", "Save EQ Preset");
        if (string.IsNullOrWhiteSpace(name)) return;
        _eq.SavePreset(name.Trim());
    }

    private void RenamePreset(AudioForge.UI.Data.EqPresetRecord rec)
    {
        string? name = InputDialog.Show(_display.FindForm(), "Enter new preset name:", "Rename EQ Preset", rec.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == rec.Name) return;
        _eq.RenamePreset(rec.Id, name.Trim());
    }

    private void ExportCurrent()
    {
        using var dlg = new SaveFileDialog { Filter = "EQ preset (*.eqp.json)|*.eqp.json|JSON (*.json)|*.json", FileName = "eq-preset.json" };
        if (dlg.ShowDialog(_display.FindForm()) != DialogResult.OK) return;
        var doc = new { GainsDb = _eq.BandGains, InputGainDb = _eq.InputGainDb, OutputGainDb = _eq.OutputGainDb };
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ImportEq()
    {
        using var dlg = new OpenFileDialog { Filter = "EQ preset (*.eqp.json;*.json)|*.eqp.json;*.json|All files|*.*" };
        if (dlg.ShowDialog(_display.FindForm()) != DialogResult.OK) return;
        try
        {
            var doc = JsonSerializer.Deserialize<ImportDoc>(File.ReadAllText(dlg.FileName));
            if (doc?.GainsDb is null || doc.GainsDb.Length != 31) { Info("Import EQ", "File does not contain a valid 31-band preset."); return; }
            _eq.SetAllGains(doc.GainsDb);
            _eq.InputGainDb = doc.InputGainDb;
            _eq.OutputGainDb = doc.OutputGainDb;
        }
        catch (Exception ex)
        {
            Info("Import EQ", $"Could not read preset:\n{ex.Message}");
        }
    }

    private async Task AnalyzeFile()
    {
        using var dlg = new OpenFileDialog { Filter = "Audio files|*.wav;*.mp3;*.flac;*.ogg;*.m4a;*.aac;*.wma;*.alac;*.caf|All files|*.*" };
        if (dlg.ShowDialog(_display.FindForm()) != DialogResult.OK) return;
        try
        {
            Info("Analyzing...", "Measuring loudness and true peak. This reads the whole file.");
            Info("File Analysis", await _eq.AnalyzeFileAsync(dlg.FileName));
        }
        catch (Exception ex) { Info("File Analysis", $"Failed:\n{ex.Message}"); }
    }

    private async Task OfflineRender()
    {
        using var open = new OpenFileDialog { Filter = "Audio files|*.wav;*.mp3;*.flac;*.ogg;*.m4a;*.aac;*.wma;*.alac;*.caf|All files|*.*" };
        if (open.ShowDialog(_display.FindForm()) != DialogResult.OK) return;
        using var save = new SaveFileDialog { Filter = "WAV output (*.wav)|*.wav", FileName = Path.GetFileNameWithoutExtension(open.FileName) + "-eq.wav" };
        if (save.ShowDialog(_display.FindForm()) != DialogResult.OK) return;
        try
        {
            Info("Rendering...", "Decoding and processing through the current EQ curve offline.");
            Info("Offline Render", await _eq.RenderOfflineAsync(open.FileName, save.FileName));
        }
        catch (Exception ex) { Info("Offline Render", $"Failed:\n{ex.Message}"); }
    }
    private sealed class ImportDoc
    {
        public float[]? GainsDb { get; set; }
        public float InputGainDb { get; set; }
        public float OutputGainDb { get; set; }
    }

    // ---------- Engine ----------

    private RibbonGroupPanel BuildEngineGroup()
    {
        var menu = BuildMenu();
        AddItem(menu, "Diagnostics", () =>
        {
            var meters = MeterService.Instance;
            Info("EQ Diagnostics",
                $"Node: {_eq.Node.Name}\n" +
                $"Bands: {_eq.BandCount} (ISO 1/3-octave, constant-Q)\n" +
                $"Max band gain: ±{GraphicEqNode.MaxBandGainDb:0} dB\n" +
                $"Q: {GraphicEqNode.ThirdOctaveQ}\n" +
                $"Input meter:  peak {meters.Input.PeakDb:0.0} dBFS / RMS {meters.Input.RmsDb:0.0} dBFS\n" +
                $"Output meter: peak {meters.Output.PeakDb:0.0} dBFS / RMS {meters.Output.RmsDb:0.0} dBFS\n" +
                $"Output clip: {(meters.Output.Clip ? "YES" : "no")}");
        });

        AddItem(menu, "Export Response Curve...", () => _display.ExportResponseCurve());
        AddItem(menu, "Null Test (EQ Transparency)", () => Info("Null Test", _eq.RunNullTest()));
        AddItem(menu, "Analyze File (LUFS / True Peak)...", async () => await AnalyzeFile());
        AddItem(menu, "Offline Render (Current EQ)...", async () => await OfflineRender());

        var group = new RibbonGroupPanel("Engine") { ContextMenuStrip = menu };
        group.AddButton("Engine", menu);
        return group;
    }
}
