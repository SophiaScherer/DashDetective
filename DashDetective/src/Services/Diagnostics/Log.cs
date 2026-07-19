using System;
using System.Globalization;
using System.IO;

namespace DashDetective.Services.Diagnostics;

/// <summary>
/// Minimal logging seam behind the app's soft-fail <c>catch</c> blocks. Writes to the debugger output
/// and a per-day rolling file under <c>%LocalAppData%/DashDetective/logs</c>. It soft-fails itself — a
/// logging error is swallowed, so <see cref="Warn"/>/<see cref="Error"/> never throw back into a caller's
/// catch block. No logging packages; deliberately tiny.
/// </summary>
public static class Log {
    private static readonly object Gate = new();
    private static readonly string? LogPath = BuildLogPath();

    /// <summary>Records a soft-failed operation (the caller still recovers). <paramref name="context"/>
    /// is a short "where/what" string.</summary>
    public static void Warn(string context, Exception? error = null) => Write("WARN", context, error);

    /// <summary>Records an unexpected error (e.g. an unhandled/unobserved exception).</summary>
    public static void Error(string context, Exception? error = null) => Write("ERROR", context, error);

    private static void Write(string level, string context, Exception? error) {
        try {
            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var line = error is null
                ? $"{stamp} [{level}] {context}"
                : $"{stamp} [{level}] {context} — {error.GetType().Name}: {error.Message}";

            System.Diagnostics.Debug.WriteLine(line);
            if (LogPath is not null) {
                lock (Gate)
                    File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        } catch {
            // Logging must never throw into a caller's catch block.
        }
    }

    private static string? BuildLogPath() {
        try {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DashDetective", "logs");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"dashdetective-{DateTime.Now:yyyyMMdd}.log");
        } catch {
            return null;
        }
    }
}
