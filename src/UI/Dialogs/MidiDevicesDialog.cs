using HelixAudio.MIDI;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicForge.UI.Dialogs;

/// <summary>
/// MIDI device panel: enumerates WinMM MIDI inputs/outputs through the
/// engine's <see cref="MidiInputDevice"/>/<see cref="MidiOutputDevice"/>,
/// sends a test note, and can monitor incoming messages.
/// </summary>
public sealed class MidiDevicesDialog : Form
{
    private static readonly Color BgDark = Color.FromArgb(28, 28, 30);
    private static readonly Color PanelDark = Color.FromArgb(38, 38, 40);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);

    private readonly ListBox _inputs = new();
    private readonly ListBox _outputs = new();
    private readonly TextBox _monitor = new();
    private readonly Button _testBtn = new();
    private readonly Button _monitorBtn = new();
    private MidiOutputDevice? _testOut;
    private MidiInputDevice? _monitorIn;

    public MidiDevicesDialog()
    {
        Text = "MIDI Devices";
        Size = new Size(640, 480);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgDark;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9F);

        Controls.Add(new Label { Text = "Inputs:", AutoSize = true, Location = new Point(12, 12), ForeColor = TextColor });
        _inputs.SetBounds(12, 32, 290, 300);
        Style(_inputs);

        Controls.Add(new Label { Text = "Outputs:", AutoSize = true, Location = new Point(322, 12), ForeColor = TextColor });
        _outputs.SetBounds(322, 32, 290, 300);
        Style(_outputs);

        RefreshDevices();

        _testBtn.Text = "Send Test Note (C4)";
        _testBtn.SetBounds(322, 344, 160, 28);
        StyleButton(_testBtn);
        _testBtn.Click += TestNote;

        _monitorBtn.Text = "Start Monitoring";
        _monitorBtn.SetBounds(490, 344, 122, 28);
        StyleButton(_monitorBtn);
        _monitorBtn.Click += ToggleMonitor;

        _monitor.SetBounds(12, 344, 290, 120);
        _monitor.Multiline = true;
        _monitor.ReadOnly = true;
        _monitor.ScrollBars = ScrollBars.Vertical;
        _monitor.BackColor = Color.Black;
        _monitor.ForeColor = Color.LightGreen;
        _monitor.Font = new Font("Consolas", 8.5F);
        _monitor.Text = "(no input selected)";

        Controls.AddRange(new Control[] { _inputs, _outputs, _testBtn, _monitorBtn, _monitor });
        FormClosed += (_, _) => StopAll();
    }

    private static void Style(ListBox lb)
    {
        lb.BackColor = PanelDark; lb.ForeColor = TextColor;
        lb.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void StyleButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat; b.ForeColor = TextColor; b.BackColor = PanelDark;
    }

    private void RefreshDevices()
    {
        _inputs.Items.Clear();
        foreach (var (id, name) in MidiInputDevice.GetDevices())
            _inputs.Items.Add($"[{id}] {name}");
        if (_inputs.Items.Count == 0) _inputs.Items.Add("(no MIDI inputs)");

        _outputs.Items.Clear();
        foreach (var (id, name) in MidiOutputDevice.GetDevices())
            _outputs.Items.Add($"[{id}] {name}");
        if (_outputs.Items.Count == 0) _outputs.Items.Add("(no MIDI outputs)");
    }

    private int SelectedIndex(ListBox lb)
    {
        if (lb.SelectedIndex < 0 || lb.SelectedItem is not string s) return -1;
        int close = s.IndexOf(']');
        return close > 0 && int.TryParse(s.AsSpan(1, close - 1), out var id) ? id : -1;
    }

    private void TestNote(object? sender, EventArgs e)
    {
        int id = SelectedIndex(_outputs);
        if (id < 0) { LogMonitor("Select an output device first."); return; }
        try
        {
            _testOut?.Dispose();
            _testOut = MidiOutputDevice.Open(id);
            // Note on C4 vel 100, then note off after 300 ms.
            _testOut.SendShortMessage(0x90, 60, 100);
            var t = new System.Windows.Forms.Timer { Interval = 300 };
            t.Tick += (_, _) => { try { _testOut?.SendShortMessage(0x80, 60, 0); } catch { } t.Stop(); t.Dispose(); };
            t.Start();
            LogMonitor($"Sent note-on C4 to output {id}.");
        }
        catch (Exception ex) { LogMonitor($"Send failed: {ex.Message}"); }
    }

    private bool _monitoring;

    private void ToggleMonitor(object? sender, EventArgs e)
    {
        if (_monitoring) { StopMonitor(); return; }
        int id = SelectedIndex(_inputs);
        if (id < 0) { LogMonitor("Select an input device first."); return; }
        try
        {
            _monitorIn?.Dispose();
            _monitorIn = MidiInputDevice.Open(id);
            _monitorIn.MessageReceived += OnMidiMessage;
            _monitorIn.Error += m => BeginInvoke(() => LogMonitor($"device error: {m}"));
            _monitoring = true;
            _monitorBtn.Text = "Stop Monitoring";
            _monitor.Text = "";
            LogMonitor($"Monitoring input {id} - play some notes…");
        }
        catch (Exception ex) { LogMonitor($"Open failed: {ex.Message}"); }
    }

    private void OnMidiMessage(MidiEvent evt)
    {
        // MidiEvent fires on a WinMM callback thread - marshal to UI.
        try { BeginInvoke(() => LogMonitor($"{evt.Type} ch{evt.Channel} d1={evt.Data1} d2={evt.Data2}")); }
        catch { }
    }

    private void StopMonitor()
    {
        _monitoring = false;
        _monitorBtn.Text = "Start Monitoring";
        _monitorIn?.Dispose();
        _monitorIn = null;
    }

    private void StopAll()
    {
        StopMonitor();
        _testOut?.Dispose();
        _testOut = null;
    }

    private void LogMonitor(string msg)
    {
        if (InvokeRequired) { BeginInvoke(() => LogMonitor(msg)); return; }
        if (_monitor.Text == "(no input selected)") _monitor.Text = "";
        _monitor.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {msg}\r\n");
    }
}
