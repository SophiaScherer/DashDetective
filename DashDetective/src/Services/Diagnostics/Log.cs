using System;
using System.Globalization;
using System.IO;

namespace DashDetective.Services.Diagnostics;

/// <summary>
/// Minimal logging seam behind the app's soft-fail <c>catch</c> blocks. Writes to the debugger output
/// and a per-day rolling file under <c>%LocalAppData%/DashDetective/logs</c> (on Linux,
/// <c>$XDG_DATA_HOME</c> ?? <c>~/.local/share</c>). It soft-fails itself — a
/// logging error is swallowed, so <see cref="Warn"/>/<see cref="Error"/> never throw back into a caller's
/// catch block. No logging packages; deliberately tiny.
/// </summary>
public static class Log {
    private static readonly object Gate = new();
    private static readonly string? LogDirectory = BuildLogDirectory();

    /// <summary>Records an expected, ordinary outcome worth a trace (e.g. no IPv6 route on a v4-only network).</summary>
    public static void Info(string context, Exception? error = null) => Write("INFO", context, error);

    /// <summary>Records a soft-failed operation (the caller still recovers). <paramref name="context"/>
    /// is a short "where/what" string.</summary>
    public static void Warn(string context, Exception? error = null) => Write("WARN", context, error);

    /// <summary>Records an unexpected error (e.g. an unhandled/unobserved exception).</summary>
    public static void Error(string context, Exception? error = null) => Write("ERROR", context, error);

    private static void Write(string level, string context, Exception? error) {
        try {
            var now = DateTime.Now;
            var stamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var line = error is null
                ? $"{stamp} [{level}] {context}"
                : $"{stamp} [{level}] {context} — {error.GetType().Name}: {error.Message}";

            System.Diagnostics.Debug.WriteLine(line);
            if (LogDirectory is not null) {
                lock (Gate)
                    Append(LogDirectory, now, line);
            }
        } catch {
            // Logging must never throw into a caller's catch block.
        }
    }

    /// <summary>The file a line stamped <paramref name="day"/> belongs in. Chosen per write so a session
    /// running past midnight rolls to a new day's file.</summary>
    internal static string FilePath(string directory, DateTime day) =>
        Path.Combine(directory, $"dashdetective-{day:yyyyMMdd}.log");

    internal static void Append(string directory, DateTime day, string line) =>
        File.AppendAllText(FilePath(directory, day), line + Environment.NewLine);

    // LocalApplicationData resolves to $XDG_DATA_HOME ?? ~/.local/share on Linux — a different tree
    // from SettingsStore's $XDG_CONFIG_HOME, so the log and settings.json never collide.
    private static string? BuildLogDirectory() {
        try {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DashDetective", "logs");
            Directory.CreateDirectory(dir);
            return dir;
        } catch {
            return null;
        }
    }
}
