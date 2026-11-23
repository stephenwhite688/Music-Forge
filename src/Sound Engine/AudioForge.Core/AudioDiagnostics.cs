using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AudioForge.Core;

/// <summary>Rate-limited diagnostics shared by the engine, backends, and UI.</summary>
public static class AudioDiagnostics
{
    private static readonly object FileGate = new();
    private static readonly ConcurrentDictionary<string, long> LastMessages = new();
    private static readonly string _logDirectory = ResolveLogDirectory();
    private static readonly string _logFilePath = Path.Combine(_logDirectory, "audio-engine.log");

    static AudioDiagnostics()
    {
        Directory.CreateDirectory(_logDirectory);
        Log("Diagnostics", $"Initialized; log directory={_logDirectory}");
    }

    public static string LogDirectory => _logDirectory;
    public static string LogFilePath => _logFilePath;

    /// <summary>When false, Log/LogError calls become no-ops (crash.log still written by the app).</summary>
    public static bool Enabled { get; set; } = true;

    public static void Log(string subsystem, string message)
    {
        if (!Enabled) return;
        var line = $"[{DateTimeOffset.UtcNow:O}] [{subsystem}] {message}{Environment.NewLine}";
        try
        {
            lock (FileGate)
                File.AppendAllText(_logFilePath, line, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Audio diagnostics write failed: {ex.Message}");
        }
    }

    public static void LogError(string subsystem, string message, Exception? exception = null)
    {
        var detail = exception == null ? string.Empty : $" exception={exception.GetType().Name}: {exception.Message}";
        Log(subsystem, $"ERROR {message}{detail}");
    }

    public static void LogRateLimited(string key, string subsystem, string message, TimeSpan interval)
    {
        var now = Stopwatch.GetTimestamp();
        var minimumTicks = Math.Max(1L, (long)(interval.TotalSeconds * Stopwatch.Frequency));
        var previous = LastMessages.GetOrAdd(key, 0);
        if (now - previous < minimumTicks)
            return;

        LastMessages[key] = now;
        Log(subsystem, message);
    }

    private static string ResolveLogDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("AUDIOFORGE_AUDIO_LOG_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && directory != null; i++, directory = directory.Parent)
        {
            var dir = directory.FullName;
            if (File.Exists(Path.Combine(dir, "AudioForge.sln")) ||
                File.Exists(Path.Combine(dir, "AudioForge.SoundEngine.slnx")) ||
                File.Exists(Path.Combine(dir, "AudioForge.SoundEngine.sln")))
                return Path.Combine(dir, "logs");
        }

        // App-agnostic fallback: use the host app's assembly name so each
        // consumer (MusicForge, a future DAW, the forensics tool) gets its
        // own log folder under %LOCALAPPDATA%.
        var appName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        if (string.IsNullOrWhiteSpace(appName)) appName = "AudioForge";
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName,
            "logs");
    }
}
