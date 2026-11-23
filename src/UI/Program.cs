using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using HelixAudio.Core;
using MusicForge.UI.Data;
using MusicForge.UI.MainWindow;

namespace MusicForge.UI;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        InstallCrashHandlers();
        ApplicationConfiguration.Initialize();
        MusicForgeDatabase.Instance.Initialize();
        Application.Run(new MainForm());
    }

    /// <summary>
    /// Last line of defence: every unhandled exception is written to the
    /// engine diagnostics log AND a dedicated crash log next to it, then
    /// surfaced to the user instead of dying silently - critical for the
    /// background audio feed thread where no UI is involved.
    /// </summary>
    private static void InstallCrashHandlers()
    {
        // UI thread exceptions - recoverable, app keeps running.
        Application.ThreadException += (_, e) =>
        {
            LogCrash("UI thread", e.Exception);
            MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}\n\nDetails were written to the log.",
                "MusicForge", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        // Background thread exceptions (incl. the audio feed thread) and
        // finally-block failures - the process is about to die; log first.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                LogCrash(e.IsTerminating ? "fatal" : "background", ex);
        };

        // Fire-and-forget task failures.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("unobserved task", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogCrash(string origin, Exception ex)
    {
        try
        {
            AudioDiagnostics.LogError("Crash", $"{origin}: {ex}", ex);
            string crashLog = Path.Combine(AudioDiagnostics.LogDirectory, "crash.log");
            File.AppendAllText(crashLog,
                $"[{DateTimeOffset.Now:O}] [{origin}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* never throw from a crash handler */ }
    }
}
