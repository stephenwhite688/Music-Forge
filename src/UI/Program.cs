using System;
using System.Windows.Forms;
using AudioForge.UI.Data;
using AudioForge.UI.MainWindow;

namespace AudioForge.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        AudioForgeDatabase.Instance.Initialize();
        Application.Run(new MainForm());
    }
}
