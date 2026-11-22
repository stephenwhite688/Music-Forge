using HelixAudio.IO.Codecs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AudioForge.UI.Dialogs;

/// <summary>
/// Batch audio converter. Decodes each source through
/// <see cref="CodecManager"/> and re-encodes through the target codec's
/// <see cref="IAudioCodec.WriteAsync"/> - no UI-side DSP or codec code.
/// </summary>
public sealed class ConvertDialog : Form
{
    private static readonly Color BgDark = Color.FromArgb(28, 28, 30);
    private static readonly Color PanelDark = Color.FromArgb(38, 38, 40);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);

    private readonly ListBox _files = new();
    private readonly ComboBox _format = new();
    private readonly TextBox _destFolder = new();
    private readonly Button _convert = new();
    private readonly ProgressBar _progress = new();
    private readonly ListBox _log = new();
    private CancellationTokenSource? _cts;

    private static readonly (string Label, string Ext, int Bitrate)[] Formats =
    {
        ("MP3 - 320 kbps", ".mp3", 320000),
        ("AAC / M4A - 256 kbps", ".m4a", 256000),
        ("OGG Vorbis - 256 kbps", ".ogg", 256000),
        ("WAV - PCM 16-bit", ".wav", 0),
    };

    public ConvertDialog()
    {
        Text = "Convert / Export Audio";
        Size = new Size(720, 520);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgDark;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9F);

        var fileLabel = new Label { Text = "Files:", AutoSize = true, Location = new Point(12, 12), ForeColor = TextColor };
        _files.SetBounds(12, 32, 560, 200);
        _files.BackColor = PanelDark;
        _files.ForeColor = TextColor;
        _files.SelectionMode = SelectionMode.MultiExtended;

        var addBtn = MakeButton("Add Files…", new Point(580, 32));
        addBtn.Click += AddFiles;
        var removeBtn = MakeButton("Remove", new Point(580, 62));
        removeBtn.Click += (_, _) => { foreach (var i in _files.SelectedIndices.Cast<int>().OrderByDescending(x => x).ToList()) _files.Items.RemoveAt(i); };

        var fmtLabel = new Label { Text = "Output format:", AutoSize = true, Location = new Point(12, 246), ForeColor = TextColor };
        _format.SetBounds(110, 242, 220, 24);
        _format.DropDownStyle = ComboBoxStyle.DropDownList;
        _format.BackColor = PanelDark;
        _format.ForeColor = TextColor;
        _format.FlatStyle = FlatStyle.Flat;
        foreach (var f in Formats) _format.Items.Add(f.Label);
        _format.SelectedIndex = 0;

        var destLabel = new Label { Text = "Destination:", AutoSize = true, Location = new Point(12, 282), ForeColor = TextColor };
        _destFolder.SetBounds(110, 278, 430, 24);
        _destFolder.BackColor = PanelDark;
        _destFolder.ForeColor = TextColor;
        _destFolder.BorderStyle = BorderStyle.FixedSingle;
        var browseBtn = MakeButton("Browse…", new Point(548, 276));
        browseBtn.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { Description = "Select output folder", SelectedPath = _destFolder.Text };
            if (d.ShowDialog(this) == DialogResult.OK) _destFolder.Text = d.SelectedPath;
        };

        _convert.SetBounds(110, 314, 140, 30);
        _convert.Text = "Convert";
        _convert.FlatStyle = FlatStyle.Flat;
        _convert.BackColor = Color.FromArgb(0, 122, 204);
        _convert.ForeColor = Color.White;
        _convert.Click += ConvertClicked;

        _progress.SetBounds(12, 356, 680, 18);
        _progress.Style = ProgressBarStyle.Continuous;

        _log.SetBounds(12, 382, 680, 110);
        _log.BackColor = Color.Black;
        _log.ForeColor = Color.LightGreen;
        _log.Font = new Font("Consolas", 8.5F);

        Controls.AddRange(new Control[] { fileLabel, _files, addBtn, removeBtn, fmtLabel, _format, destLabel, _destFolder, browseBtn, _convert, _progress, _log });
    }

    private Button MakeButton(string text, Point loc)
    {
        var b = new Button { Text = text, Location = loc, Size = new Size(120, 26), FlatStyle = FlatStyle.Flat, ForeColor = TextColor, BackColor = PanelDark };
        return b;
    }

    private void AddFiles(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio files|*.wav;*.mp3;*.m4a;*.aac;*.ogg;*.flac;*.wma;*.opus|All files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        foreach (var f in dlg.FileNames)
            if (!_files.Items.Contains(f)) _files.Items.Add(f);
        if (_destFolder.Text.Length == 0 && dlg.FileNames.Length > 0)
            _destFolder.Text = Path.GetDirectoryName(dlg.FileNames[0]) ?? "";
    }

    private async void ConvertClicked(object? sender, EventArgs e)
    {
        if (_cts is not null) { _cts.Cancel(); return; }
        var files = _files.Items.Cast<string>().ToList();
        if (files.Count == 0) { Log("No files selected."); return; }
        if (string.IsNullOrWhiteSpace(_destFolder.Text) || !Directory.Exists(_destFolder.Text))
        { Log("Destination folder does not exist."); return; }

        int fi = _format.SelectedIndex;
        var (label, ext, bitrate) = Formats[Math.Clamp(fi, 0, Formats.Length - 1)];

        _cts = new CancellationTokenSource();
        _convert.Text = "Cancel";
        _log.Items.Clear();
        int done = 0, ok = 0, failed = 0;

        foreach (var src in files)
        {
            if (_cts.IsCancellationRequested) break;
            string dst = Path.Combine(_destFolder.Text,
                Path.GetFileNameWithoutExtension(src) + ext);
            Log($"[{++done}/{files.Count}] {Path.GetFileName(src)} -> {Path.GetFileName(dst)} …");
            try
            {
                await Task.Run(async () =>
                {
                    using var stream = await CodecManager.ReadAsync(src, _cts.Token);
                    var codec = CodecManager.GetCodecForExtension(dst)
                        ?? throw new InvalidOperationException($"No encoder for '{ext}'");
                    if (!codec.CanWrite)
                        throw new InvalidOperationException($"Codec '{codec.Name}' cannot write");
                    await codec.WriteAsync(dst, stream, _cts.Token);
                }, _cts.Token);
                ok++;
                Log($"    OK ({new FileInfo(dst).Length / 1024} KB)");
            }
            catch (OperationCanceledException) { Log("    cancelled"); break; }
            catch (Exception ex) { failed++; Log($"    FAIL: {ex.Message}"); }
            _progress.Value = (int)(done * 100.0 / files.Count);
        }

        Log($"Done: {ok} converted, {failed} failed.");
        _cts.Dispose(); _cts = null;
        _convert.Text = "Convert";
        _progress.Value = 0;
    }

    private void Log(string msg)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(msg)); return; }
        _log.Items.Add(msg);
        _log.TopIndex = _log.Items.Count - 1;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosed(e);
    }
}
