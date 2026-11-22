using System;
using System.Windows.Forms;
using MusicForge.UI.Data;
using MusicForge.UI.MainWindow;

namespace MusicForge.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        MusicForgeDatabase.Instance.Initialize();
        Application.Run(new MainForm());
    }
}
