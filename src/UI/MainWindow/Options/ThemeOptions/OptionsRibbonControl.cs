#nullable enable

using System.Windows.Forms;
using MusicForge.UI.MainWindow.Ribbons;

namespace MusicForge.UI.MainWindow.Options.ThemeOptions
{
    public partial class OptionsRibbonControl : RibbonStripControl
    {
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
            var filesGroup = BuildFilesGroup();
            var performanceGroup = BuildPerformanceGroup();
            var helpGroup = BuildHelpGroup();
            var aboutGroup = BuildAboutGroup();

            // WinForms docks later-added controls closer to the edge, so add in
            // reverse order to keep Appearance leftmost and About rightmost.
            foreach (var group in new[] { aboutGroup, helpGroup, performanceGroup, filesGroup, startupGroup, interfaceGroup, appearanceGroup })
            {
                group.Dock = DockStyle.Left;
                RibbonPanel.Controls.Add(group);
            }
        }

        private static ContextMenuStrip BuildMenu(params string[] items) => RibbonMenuFactory.Create(items);


        private static RibbonGroupPanel BuildAppearanceGroup()
        {
            var menu = BuildMenu("Dark Theme", "Light Theme", "Blue Theme", "Green Theme", "Red Theme",
                "Custom Theme", "Import Theme", "Export Theme", "Reset Theme");
            var group = new RibbonGroupPanel("Appearance") { ContextMenuStrip = menu };
            group.AddButton("Themes", menu);
            group.AddButton("Colours", menu);
            group.AddButton("Fonts", menu);
            group.AddButton("Icons", menu);
            group.AddButton("Reset", menu);
            return group;
        }

        private static RibbonGroupPanel BuildInterfaceGroup()
        {
            var menu = BuildMenu("Compact Layout", "Standard Layout", "Studio Layout", "Advanced Layout",
                "Show Tooltips", "Hide Tooltips", "Reset Window Layout", "Reset Ribbon");
            var group = new RibbonGroupPanel("Interface") { ContextMenuStrip = menu };
            group.AddButton("Layout", menu);
            group.AddButton("Windows", menu);
            group.AddButton("Ribbon", menu);
            group.AddButton("Tooltips", menu);
            group.AddButton("Accessibility", menu);
            return group;
        }

        private static RibbonGroupPanel BuildStartupGroup()
        {
            var menu = BuildMenu("Start Empty", "Open Last Session", "Open Media", "Open Radio",
                "Open Visualiser", "Open Mixer", "Load Workspace", "Save Workspace");
            var group = new RibbonGroupPanel("Startup") { ContextMenuStrip = menu };
            group.AddButton("Startup", menu);
            group.AddButton("Workspace", menu);
            group.AddButton("Sessions", menu);
            group.AddButton("Profiles", menu);
            group.AddButton("Defaults", menu);
            return group;
        }

        private static RibbonGroupPanel BuildFilesGroup()
        {
            var menu = BuildMenu("Media Locations", "Library Locations", "Backup Now", "Restore Backup",
                "Import Settings", "Export Settings", "Import Profile", "Export Profile");
            var group = new RibbonGroupPanel("Files") { ContextMenuStrip = menu };
            group.AddButton("Media Paths", menu);
            group.AddButton("Library", menu);
            group.AddButton("Backup", menu);
            group.AddButton("Import", menu);
            group.AddButton("Export", menu);
            return group;
        }

        private static RibbonGroupPanel BuildPerformanceGroup()
        {
            var menu = BuildMenu("Low Resource Mode", "Balanced Mode", "High Performance Mode",
                "Clear Cache", "Enable Logging", "Disable Logging", "View Diagnostics");
            var group = new RibbonGroupPanel("Performance") { ContextMenuStrip = menu };
            group.AddButton("Performance", menu);
            group.AddButton("Memory", menu);
            group.AddButton("Cache", menu);
            group.AddButton("Logging", menu);
            group.AddButton("Diagnostics", menu);
            return group;
        }

        private static RibbonGroupPanel BuildHelpGroup()
        {
            var menu = BuildMenu("Getting Started", "Media Guide", "Radio Guide", "Mixer Guide",
                "Visualiser Guide", "Audio Engine Guide", "Troubleshooting",
                "Frequently Asked Questions");
            var group = new RibbonGroupPanel("Help") { ContextMenuStrip = menu };
            group.AddButton("Contents", menu);
            group.AddButton("Search", menu);
            group.AddButton("Tutorials", menu);
            group.AddButton("FAQ", menu);
            group.AddButton("Support", menu);
            return group;
        }

        private static RibbonGroupPanel BuildAboutGroup()
        {
            var menu = BuildMenu("About Audio Forge", "Send Email", "Credits",
                "Licence Information", "Version Information", "Change Log");
            var group = new RibbonGroupPanel("About") { ContextMenuStrip = menu };
            group.AddButton("About", menu);
            group.AddButton("Contact", menu);
            group.AddButton("Author", menu);
            group.AddButton("Credits", menu);
            group.AddButton("Licence", menu);
            group.AddButton("Version", menu);
            return group;
        }
    }
}
