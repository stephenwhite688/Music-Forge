using MusicForge.UI.Services;
using AudioForge.Mixing;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicForge.UI.Mixer;

/// <summary>
/// Stereo mixer panel: per-channel gain slider, mute/solo, live level meter,
/// plus a stereo-balance control. All controls drive the engine's
/// <see cref="StereoMixerNode"/> in the playback chain live.
/// </summary>
public class MixerPanelControl : UserControl
{
    private readonly Panel _canvas = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer;
    private readonly TrackBar _balance;
    private readonly Label _balanceLabel;

    private sealed class ChannelStrip
    {
        public required int Index;
        public required string Name;
        public required TrackBar Fader;
        public required Button Mute;
        public required Button Solo;
        public required Panel Meter;
    }

    private readonly ChannelStrip[] _strips;

    public MixerPanelControl()
    {
        BackColor = Color.FromArgb(30, 30, 34);
        ForeColor = Color.White;

        _canvas.Paint += Canvas_Paint;
        Controls.Add(_canvas);

        var mixer = MeterService.Instance.Mixer;
        _strips = new[]
        {
            CreateStrip(mixer, 0, "L"),
            CreateStrip(mixer, 1, "R")
        };

        // Balance control along the bottom
        _balanceLabel = new Label
        {
            Text = "Balance",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 8F),
            AutoSize = true
        };
        _balance = new TrackBar
        {
            Minimum = -100, Maximum = 100, Value = 0,
            TickFrequency = 25,
            Width = 400, Height = 30,
            BackColor = Color.FromArgb(45, 45, 50),
            ForeColor = Color.White
        };
        _balance.ValueChanged += (_, _) => mixer.Balance = _balance.Value / 100f;
        var balancePanel = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(38, 38, 42) };
        _balanceLabel.Location = new Point(8, 8);
        _balance.Location = new Point(70, 4);
        balancePanel.Controls.Add(_balanceLabel);
        balancePanel.Controls.Add(_balance);
        Controls.Add(balancePanel);
        balancePanel.BringToFront();

        _timer = new System.Windows.Forms.Timer { Interval = 50 };
        _timer.Tick += (_, _) => _canvas.Invalidate();
        _timer.Start();
    }

    private ChannelStrip CreateStrip(StereoMixerNode mixer, int index, string name)
    {
        var strip = mixer.Strips[index];
        var panel = new Panel
        {
            Width = 90, Height = 300,
            BackColor = Color.FromArgb(40, 40, 45),
            Margin = new Padding(6)
        };
        panel.Paint += StripPanel_Paint;

        var label = new Label
        {
            Text = name,
            Dock = DockStyle.Top, Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        var fader = new TrackBar
        {
            Minimum = 0, Maximum = 200, Value = 100, // percent
            Orientation = Orientation.Vertical,
            Height = 200, Width = 30
        };
        fader.Scroll += (_, _) => strip.Gain = fader.Value / 100f;

        var mute = new Button { Text = "M", Width = 34, Height = 22, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 60, 66) };
        mute.FlatAppearance.BorderSize = 0;
        mute.Click += (_, _) => { strip.Mute = !strip.Mute; mute.BackColor = strip.Mute ? Color.DarkRed : Color.FromArgb(60, 60, 66); };

        var solo = new Button { Text = "S", Width = 34, Height = 22, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 60, 66) };
        solo.FlatAppearance.BorderSize = 0;
        solo.Click += (_, _) => { strip.Solo = !strip.Solo; solo.BackColor = strip.Solo ? Color.DarkGoldenrod : Color.FromArgb(60, 60, 66); };

        var meter = new Panel { Width = 8, Height = 200, BackColor = Color.Black };
        meter.Paint += (_, e) => DrawMeter(e.Graphics, meter, index);

        // Layout: label top, meter left, fader center, M/S bottom
        label.Dock = DockStyle.Top;
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 28, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.FromArgb(40, 40, 45) };
        bottom.Controls.Add(mute); bottom.Controls.Add(solo);
        var faderWrap = new Panel { Width = 36, Height = 200, BackColor = Color.FromArgb(40, 40, 45) };
        faderWrap.Controls.Add(fader);
        fader.Location = new Point(3, 0);
        faderWrap.Controls.Add(meter);
        meter.Location = new Point(32, 0);
        faderWrap.Dock = DockStyle.Left;
        bottom.Dock = DockStyle.Bottom;
        panel.Controls.Add(label);
        panel.Controls.Add(faderWrap);
        panel.Controls.Add(bottom);

        // Position strips manually on the canvas
        int x = 10 + index * 100;
        panel.Location = new Point(x, 10);
        panel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _canvas.Controls.Add(panel);

        return new ChannelStrip { Index = index, Name = name, Fader = fader, Mute = mute, Solo = solo, Meter = meter };
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        foreach (var s in _strips) s.Meter.Invalidate();
    }

    private void DrawMeter(Graphics g, Control meter, int channel)
    {
        var levels = MeterService.Instance.Mixer.RmsDbPerChannel;
        float db = channel < levels.Length ? levels[channel] : -120f;
        float t = Math.Clamp((db + 60f) / 60f, 0f, 1f);
        int h = (int)(meter.Height * t);
        using var bg = new SolidBrush(Color.FromArgb(15, 15, 18));
        using var fg = new SolidBrush(db > -3f ? Color.Red : db > -12f ? Color.Orange : Color.LimeGreen);
        g.FillRectangle(bg, 0, 0, meter.Width, meter.Height);
        g.FillRectangle(fg, 0, meter.Height - h, meter.Width, h);
        // peak cap
        g.FillRectangle(Brushes.White, 0, Math.Max(0, meter.Height - h - 2), meter.Width, 2);
    }

    private void StripPanel_Paint(object? sender, PaintEventArgs e)
    {
        // subtle border
        using var pen = new Pen(Color.FromArgb(60, 60, 66));
        e.Graphics.DrawRectangle(pen, 0, 0, ((Control)sender!).Width - 1, ((Control)sender!).Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer?.Dispose();
        base.Dispose(disposing);
    }
}
