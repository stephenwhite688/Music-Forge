#nullable enable

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HelixAudio.Core;
using MusicForge.UI.Data;
using MusicForge.UI.MainWindow.Ribbons;
using MusicForge.UI.Shared;

namespace MusicForge.UI.MainWindow.Options.ThemeOptions
{
    public partial class OptionsRibbonControl : RibbonStripControl
    {
        private static string? _activePreset;

        public OptionsRibbonControl()
        {
            InitializeComponent();
            BuildRibbon();
        }

        private void BuildRibbon()
        {
            var appearanceGroup = BuildAppearanceGroup();
            var interfaceGroup = BuildInterfaceGroup();
            var startupGroup = BuildStartupGroup();
            var toolsGroup = BuildToolsGroup();
            var helpGroup = BuildHelpGroup();
            var aboutGroup = BuildAboutGroup();

            // WinForms docks later-added controls closer to the edge, so add in
            // reverse order to keep Appearance leftmost and About rightmost.
            foreach (var group in new[] { aboutGroup, helpGroup, toolsGroup, startupGroup, interfaceGroup, appearanceGroup })
            {
                group.Dock = DockStyle.Left;
                RibbonPanel.Controls.Add(group);
            }
        }

        private static void AddMenuItem(ContextMenuStrip menu, string text, Action handler, bool enabled = true)
        {
            var item = new ToolStripMenuItem(text) { Enabled = enabled };
            item.Click += (_, _) => handler();
            menu.Items.Add(item);
        }

        // Live check-state: each menu remembers its check getters and refreshes
        // them every time the menu opens, so checkmarks always reflect reality.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            ContextMenuStrip, System.Collections.Generic.List<(ToolStripMenuItem Item, Func<bool> Get)>> _checkItems = new();

        private static void AddCheckItem(ContextMenuStrip menu, string text, Func<bool> isChecked, Action handler)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) => handler();
            menu.Items.Add(item);
            var list = _checkItems.GetOrCreateValue(menu);
            list.Add((item, isChecked));
            menu.Opening += (_, _) =>
            {
                foreach (var (i, get) in list) i.Checked = get();
            };
        }

        // ==================== APPEARANCE ====================

        private RibbonGroupPanel BuildAppearanceGroup()
        {
            var group = new RibbonGroupPanel("Appearance");
            group.AddButton("Themes", BuildThemesMenu());
            group.AddButton("Colours", BuildColoursMenu());
            group.AddButton("Fonts", BuildFontsMenu());
            group.AddButton("Icons", BuildIconsMenu());
            group.AddButton("Reset", BuildResetThemeMenu());
            return group;
        }

        private ContextMenuStrip BuildResetThemeMenu()
        {
            var menu = new ContextMenuStrip();
            AddMenuItem(menu, "Reset Theme to Dark Default", () => ApplyPreset("Dark", ThemePresets.Dark));
            AddMenuItem(menu, "Reset Font", () =>
            {
                _activePreset = null;
                ThemeManager.UpdateSingleColor(t => { t.FontName = "Segoe UI"; t.FontSizeStandard = 9F; });
            });
            return menu;
        }

        private ContextMenuStrip BuildThemesMenu()
        {
            var menu = new ContextMenuStrip();
            AddCheckItem(menu, "Dark", () => _activePreset == "Dark", () => ApplyPreset("Dark", ThemePresets.Dark));
            AddCheckItem(menu, "Light", () => _activePreset == "Light", () => ApplyPreset("Light", ThemePresets.Light));
            AddCheckItem(menu, "Blue", () => _activePreset == "Blue", () => ApplyPreset("Blue", ThemePresets.Blue));
            AddCheckItem(menu, "Green", () => _activePreset == "Green", () => ApplyPreset("Green", ThemePresets.Green));
            AddCheckItem(menu, "Red", () => _activePreset == "Red", () => ApplyPreset("Red", ThemePresets.Red));
            AddCheckItem(menu, "High Contrast", () => _activePreset == "HighContrast", () => ApplyPreset("HighContrast", ThemePresets.HighContrast));
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Customise Colours…", PickThemeColour);
            AddMenuItem(menu, "Customise Font…", PickThemeFont);
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Import Theme…", ImportTheme);
            AddMenuItem(menu, "Export Theme…", ExportTheme);
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Reset to Default", () => ApplyPreset("Dark", ThemePresets.Dark));
            return menu;
        }

        private void ApplyPreset(string name, Func<ThemeModel> factory)
        {
            _activePreset = name;
            ThemeManager.ApplyCustomTheme(factory());
        }

        private ContextMenuStrip BuildColoursMenu()
        {
            var menu = new ContextMenuStrip();
            AddColourItem(menu, "Window Background", t => t.WindowBackground, (t, c) => t.WindowBackground = c);
            AddColourItem(menu, "Control Surface", t => t.ControlDefaultSurface, (t, c) => t.ControlDefaultSurface = c);
            AddColourItem(menu, "Primary Accent", t => t.PrimaryAccent, (t, c) => t.PrimaryAccent = c);
            AddColourItem(menu, "Highlight", t => t.HighlightColor, (t, c) => t.HighlightColor = c);
            AddColourItem(menu, "Text Colour", t => t.TextPrimary, (t, c) => t.TextPrimary = c);
            AddColourItem(menu, "Border", t => t.BorderColor, (t, c) => t.BorderColor = c);
            AddColourItem(menu, "EQ Bar Colour", t => t.EqualizerBarActive, (t, c) => t.EqualizerBarActive = c);
            AddColourItem(menu, "Visualiser Line", t => t.OscilloscopeLine, (t, c) => t.OscilloscopeLine = c);
            AddColourItem(menu, "Waveform Colour", t => t.WaveformPrimary, (t, c) => t.WaveformPrimary = c);
            return menu;
        }

        private void AddColourItem(ContextMenuStrip menu, string label,
            Func<ThemeModel, Color> get, Action<ThemeModel, Color> set)
        {
            var item = new ToolStripMenuItem(label);
            item.Click += (_, _) =>
            {
                using var dlg = new ColorDialog { Color = get(ThemeManager.Current), FullOpen = true };
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _activePreset = null;
                    ThemeManager.UpdateSingleColor(t => set(t, dlg.Color));
                }
            };
            menu.Items.Add(item);
        }

        private ContextMenuStrip BuildFontsMenu()
        {
            var menu = new ContextMenuStrip();
            AddMenuItem(menu, "Choose Font…", PickThemeFont);
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Larger Text", () => AdjustFontSize(1F));
            AddMenuItem(menu, "Smaller Text", () => AdjustFontSize(-1F));
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Reset Font", () =>
            {
                _activePreset = null;
                ThemeManager.UpdateSingleColor(t => { t.FontName = "Segoe UI"; t.FontSizeStandard = 9F; });
            });
            return menu;
        }

        private void AdjustFontSize(float delta)
        {
            _activePreset = null;
            ThemeManager.UpdateSingleColor(t =>
            {
                t.FontSizeStandard = Math.Clamp(t.FontSizeStandard + delta, 7F, 18F);
                t.FontSizeHeader = Math.Max(7F, t.FontSizeHeader + delta);
                t.FontSizeDigital = Math.Max(7F, t.FontSizeDigital + delta);
            });
        }

        private void PickThemeFont()
        {
            using var dlg = new FontDialog
            {
                Font = ThemeApplier.MakeFont(ThemeManager.Current),
                ShowEffects = false
            };
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _activePreset = null;
                var f = dlg.Font;
                ThemeManager.UpdateSingleColor(t =>
                {
                    t.FontName = f.FontFamily.Name;
                    t.FontSizeStandard = f.Size;
                });
            }
        }

        private void PickThemeColour()
        {
            // Opens the Colours menu equivalent - accent picker as a quick entry point
            using var dlg = new ColorDialog { Color = ThemeManager.Current.PrimaryAccent, FullOpen = true };
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _activePreset = null;
                ThemeManager.UpdateSingleColor(t => t.PrimaryAccent = dlg.Color);
            }
        }

        private ContextMenuStrip BuildIconsMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Icon packs") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "About icons", () => AppMessageBox.Show(
                "MusicForge currently ships a single icon set.\n\n" +
                "Custom icon packs are planned - themes will be able to select\n" +
                "alternate artwork for transport, ribbon, and toolbar icons.",
                "Icons", MessageBoxButtons.OK, MessageBoxIcon.Information));
            return menu;
        }

        private void ImportTheme()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Import Theme",
                Filter = "MusicForge Theme (*.mf-theme.json;*.json)|*.mf-theme.json;*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                var theme = ThemeSerializer.FromJson(File.ReadAllText(dlg.FileName));
                if (theme is null)
                {
                    AppMessageBox.Show("That file is not a valid MusicForge theme.", "Import Theme",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _activePreset = null;
                ThemeManager.ApplyCustomTheme(theme);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Could not import theme: {ex.Message}", "Import Theme",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportTheme()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Export Theme",
                Filter = "MusicForge Theme (*.mf-theme.json)|*.mf-theme.json|JSON (*.json)|*.json",
                FileName = "musicforge-theme.mf-theme.json"
            };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dlg.FileName, ThemeSerializer.ToJson(ThemeManager.Current));
                AppMessageBox.Show($"Theme exported to:\n{dlg.FileName}", "Export Theme",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Could not export theme: {ex.Message}", "Export Theme",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== INTERFACE ====================

        private RibbonGroupPanel BuildInterfaceGroup()
        {
            var group = new RibbonGroupPanel("Interface");
            group.AddButton("Layout", BuildLayoutMenu());
            group.AddButton("Tooltips & Hover", BuildTooltipsMenu());
            group.AddButton("Reset", BuildResetMenu());
            return group;
        }

        private ContextMenuStrip BuildLayoutMenu()
        {
            var menu = new ContextMenuStrip();
            AddCheckItem(menu, "Compact", () => Math.Abs(ThemeManager.Current.FontSizeStandard - 8F) < 0.5F, () => SetDensity("Compact", 8F));
            AddCheckItem(menu, "Standard", () => Math.Abs(ThemeManager.Current.FontSizeStandard - 9F) < 0.5F, () => SetDensity("Standard", 9F));
            AddCheckItem(menu, "Comfortable", () => Math.Abs(ThemeManager.Current.FontSizeStandard - 10.5F) < 0.5F, () => SetDensity("Comfortable", 10.5F));
            return menu;
        }

        private void SetDensity(string name, float size)
        {
            _activePreset = null;
            ThemeManager.UpdateSingleColor(t =>
            {
                t.FontSizeStandard = size;
                t.FontSizeHeader = size + 1F;
                t.FontSizeDigital = size + 1F;
            });
            try { new SettingsRepository().Set("InterfaceLayout", name); } catch { }
        }

        private ContextMenuStrip BuildTooltipsMenu()
        {
            var menu = new ContextMenuStrip();
            AddCheckItem(menu, "Show Tooltips", () => ThemeManager.Current.GlobalToolTipsEnabled, () =>
            {
                ThemeManager.UpdateSingleColor(t => t.GlobalToolTipsEnabled = !t.GlobalToolTipsEnabled);
            });
            menu.Items.Add(new ToolStripSeparator());
            AddCheckItem(menu, "Mouse-Over Effects", () => ThemeManager.Current.MouseOverEffectsEnabled, () =>
            {
                ThemeManager.UpdateSingleColor(t => t.MouseOverEffectsEnabled = !t.MouseOverEffectsEnabled);
            });
            return menu;
        }

        private ContextMenuStrip BuildResetMenu()
        {
            var menu = new ContextMenuStrip();
            AddMenuItem(menu, "Reset Window Layout", () =>
            {
                try
                {
                    new SettingsRepository().Set("LastOpenedMode", null);
                    if (FindForm() is MainForm mf) mf.MainTabs.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    AppMessageBox.Show($"Could not reset layout: {ex.Message}", "Options",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
            AddMenuItem(menu, "Reset Ribbon to Defaults", () =>
            {
                // Ribbon groups are code-defined; re-firing the theme event restores default styling.
                ThemeManager.ApplyCustomTheme(ThemeManager.Current);
            });
            return menu;
        }

        // ==================== STARTUP ====================

        private RibbonGroupPanel BuildStartupGroup()
        {
            var menu = new ContextMenuStrip();
            AddCheckItem(menu, "Automatic - Last Used Tab", () => GetStartupMode() is null, ClearStartupMode);
            menu.Items.Add(new ToolStripSeparator());
            AddCheckItem(menu, "Media", () => IsStartupTab("Media"), () => SetStartupTab("Media"));
            AddCheckItem(menu, "Internet Radio", () => IsStartupTab("Internet radio"), () => SetStartupTab("Internet radio"));
            AddCheckItem(menu, "Graphic Equalizer", () => IsStartupTab("Graphic Equalizer"), () => SetStartupTab("Graphic Equalizer"));
            AddCheckItem(menu, "Visualizer", () => IsStartupTab("Visualizer"), () => SetStartupTab("Visualizer"));
            AddCheckItem(menu, "Networked Speakers", () => IsStartupTab("Networked Speakers"), () => SetStartupTab("Networked Speakers"));
            AddCheckItem(menu, "Plugins", () => IsStartupTab("Plugins"), () => SetStartupTab("Plugins"));

            var group = new RibbonGroupPanel("Startup") { ContextMenuStrip = menu };
            group.AddButton("Startup Tab", menu);
            return group;
        }

        private string? GetStartupMode()
        {
            try { return new SettingsRepository().Get("StartupMode"); }
            catch { return null; }
        }

        private bool IsStartupTab(string tabText)
        {
            return string.Equals(GetStartupMode(), tabText, StringComparison.OrdinalIgnoreCase);
        }

        private void SetStartupTab(string tabText)
        {
            try { new SettingsRepository().Set("StartupMode", tabText); }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Could not save startup tab: {ex.Message}", "Options",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearStartupMode()
        {
            try { new SettingsRepository().Set("StartupMode", null); }
            catch { }
        }

        // ==================== TOOLS (backup / settings / profile / performance) ====================

        private RibbonGroupPanel BuildToolsGroup()
        {
            var menu = new ContextMenuStrip();

            AddMenuItem(menu, "Backup Library Now…", BackupDatabase);
            AddMenuItem(menu, "Restore Backup…", RestoreDatabase);
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "Import Settings…", ImportTheme);
            AddMenuItem(menu, "Export Settings…", ExportTheme);
            AddMenuItem(menu, "Export Profile (theme + layout)…", ExportProfile);
            AddMenuItem(menu, "Import Profile…", ImportTheme);
            menu.Items.Add(new ToolStripSeparator());
            AddCheckItem(menu, "Low Resource Mode", () => UiPerformance.Current == UiPerformance.Mode.Low, () => SetPerfMode(UiPerformance.Mode.Low));
            AddCheckItem(menu, "Balanced Mode", () => UiPerformance.Current == UiPerformance.Mode.Balanced, () => SetPerfMode(UiPerformance.Mode.Balanced));
            AddCheckItem(menu, "High Performance Mode", () => UiPerformance.Current == UiPerformance.Mode.High, () => SetPerfMode(UiPerformance.Mode.High));
            menu.Items.Add(new ToolStripSeparator());
            AddCheckItem(menu, "Engine Logging Enabled", () => AudioDiagnostics.Enabled, ToggleLogging);
            AddMenuItem(menu, "Clear Memory Cache", ClearCache);
            AddMenuItem(menu, "View Diagnostics Folder", OpenDiagnosticsFolder);

            var group = new RibbonGroupPanel("Tools") { ContextMenuStrip = menu };
            group.AddButton("Tools", menu);
            return group;
        }

        private void BackupDatabase()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Backup MusicForge Library",
                Filter = "MusicForge Backup (*.mfdb)|*.mfdb|All files (*.*)|*.*",
                FileName = $"musicforge-backup-{DateTime.Now:yyyyMMdd-HHmm}.mfdb"
            };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                MusicForgeDatabase.Instance.Backup(dlg.FileName);
                AppMessageBox.Show($"Library backed up to:\n{dlg.FileName}", "Backup",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Backup failed: {ex.Message}", "Backup",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreDatabase()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Restore MusicForge Library",
                Filter = "MusicForge Backup (*.mfdb;*.db)|*.mfdb;*.db|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            var confirm = AppMessageBox.Show(
                "Restoring will REPLACE the entire current library database.\n\n" +
                "Your current library will be lost. Continue?",
                "Restore Backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                MusicForgeDatabase.Instance.Restore(dlg.FileName);
                AppMessageBox.Show("Library restored. Restart MusicForge to use the restored data.",
                    "Restore Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Restore failed: {ex.Message}", "Restore Backup",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportProfile()
        {
            // A "profile" today is the theme JSON plus the layout setting - bundle both.
            using var dlg = new SaveFileDialog
            {
                Title = "Export Profile",
                Filter = "MusicForge Profile (*.mfprofile.json)|*.mfprofile.json|JSON (*.json)|*.json",
                FileName = "musicforge-profile.mfprofile.json"
            };
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
            try
            {
                var layout = "Standard";
                try { layout = new SettingsRepository().Get("InterfaceLayout") ?? "Standard"; } catch { }
                var json = "{\n  \"type\": \"MusicForgeProfile\",\n  \"version\": 1,\n" +
                           "  \"interfaceLayout\": \"" + layout + "\",\n" +
                           "  \"theme\": " + ThemeSerializer.ToJson(ThemeManager.Current) + "\n}";
                File.WriteAllText(dlg.FileName, json);
                AppMessageBox.Show($"Profile exported to:\n{dlg.FileName}", "Export Profile",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Could not export profile: {ex.Message}", "Export Profile",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetPerfMode(UiPerformance.Mode mode)
        {
            UiPerformance.Set(mode);
            if (FindForm() is MainForm mf && mf.TransportBar?.MiniVisualizer is { } mini)
                mini.Visible = mode != UiPerformance.Mode.Low;
        }

        private void ToggleLogging()
        {
            AudioDiagnostics.Enabled = !AudioDiagnostics.Enabled;
            if (AudioDiagnostics.Enabled)
                AudioDiagnostics.Log("UI", "Engine logging enabled by user");
        }

        private void ClearCache()
        {
            GC.Collect(2, GCCollectionMode.Optimized, false);
            AppMessageBox.Show("Memory cache cleared.", "Performance",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenDiagnosticsFolder()
        {
            try
            {
                Directory.CreateDirectory(AudioDiagnostics.LogDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AudioDiagnostics.LogDirectory}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppMessageBox.Show($"Could not open diagnostics folder: {ex.Message}", "Diagnostics",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ==================== HELP / ABOUT ====================

        private RibbonGroupPanel BuildHelpGroup()
        {
            var menu = new ContextMenuStrip();
            AddMenuItem(menu, "User Guide", () => HelpBrowserDialog.Open(FindForm()));
            AddMenuItem(menu, "Keyboard Shortcuts", ShowShortcuts);
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "View Diagnostics Log", OpenDiagnosticsFolder);
            AddMenuItem(menu, "Report a Problem", ReportProblem);

            var group = new RibbonGroupPanel("Help") { ContextMenuStrip = menu };
            group.AddButton("Help", menu);
            return group;
        }

        private void ReportProblem()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var version = $"{v?.Major}.{v?.Minor}.{v?.Build}";
            string subject = Uri.EscapeDataString($"MusicForge problem report - v{version}");
            string body = Uri.EscapeDataString(
                $"MusicForge v{version}\n" +
                $"OS: {Environment.OSVersion.VersionString}\n" +
                $"Diagnostics log: {AudioDiagnostics.LogFilePath}\n\n" +
                "Describe the problem below:\n");
            try
            {
                Process.Start(new ProcessStartInfo($"mailto:?subject={subject}&body={body}") { UseShellExecute = true });
            }
            catch
            {
                // No mail client - fall back to showing the info
                AppMessageBox.ShowWithLogo(FindForm(),
                    "No email client is configured.\n\n" +
                    $"Diagnostics log for your report:\n{AudioDiagnostics.LogFilePath}",
                    "Report a Problem");
            }
        }

        private void ShowShortcuts()
        {
            AppMessageBox.ShowWithLogo(FindForm(),
                "Keyboard shortcuts\n\n" +
                "Space - Play / Pause\n" +
                "Ctrl+O - Open stream URL\n" +
                "The full shortcut list grows as features are added.",
                "Keyboard Shortcuts");
        }

        private RibbonGroupPanel BuildAboutGroup()
        {
            var menu = new ContextMenuStrip();
            AddMenuItem(menu, "About MusicForge", ShowAbout);
            AddMenuItem(menu, "Version Information", ShowVersion);
            AddMenuItem(menu, "Licence Information", ShowLicence);
            AddMenuItem(menu, "Change Log", ShowChangeLog);

            var group = new RibbonGroupPanel("About") { ContextMenuStrip = menu };
            group.AddButton("About", menu);
            return group;
        }

        private void ShowAbout()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            AppMessageBox.ShowWithLogo(FindForm(),
                $"MusicForge\n\n" +
                $"Version {v?.Major}.{v?.Minor}.{v?.Build}\n\n" +
                "Your music, your library, your way.\n\n" +
                "Powered by the AudioForge sound engine.",
                "About MusicForge");
        }

        private void ShowVersion()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var v = asm.GetName().Version;
            var info = $"MusicForge {v?.Major}.{v?.Minor}.{v?.Build}\n\n" +
                       $"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}\n" +
                       $"OS: {Environment.OSVersion.VersionString}";
            AppMessageBox.ShowWithLogo(FindForm(), info, "Version Information");
        }

        private void ShowLicence()
        {
            AppMessageBox.ShowWithLogo(FindForm(),
                "MusicForge is open-source software.\n\n" +
                "Released under the MIT License - see the LICENSE file\n" +
                "in the project repository for full terms.",
                "Licence Information");
        }

        private void ShowChangeLog()
        {
            ScrollTextDialog.ShowChangeLog(FindForm());
        }
    }
}
