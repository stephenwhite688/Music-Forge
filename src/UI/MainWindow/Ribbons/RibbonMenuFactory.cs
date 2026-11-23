using System.Windows.Forms;
using MusicForge.UI.Shared;

namespace MusicForge.UI.MainWindow.Ribbons;

internal static class RibbonMenuFactory
{
    public static ContextMenuStrip Create(params string[] items)
    {
        var menu = new ContextMenuStrip();
        foreach (var item in items)
        {
            // Empty entries render as separators for visual menu grouping
            if (string.IsNullOrWhiteSpace(item))
            {
                menu.Items.Add(new ToolStripSeparator());
                continue;
            }

            var menuItem = new ToolStripMenuItem(item);
            menuItem.Click += (s, e) =>
                AppMessageBox.Show($"'{item}' is not implemented yet.", "AudioForge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            menu.Items.Add(menuItem);
        }
        return menu;
    }

}
