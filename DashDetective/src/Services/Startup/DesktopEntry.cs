using System;

namespace DashDetective.Services.Startup;

/// <summary>
/// The body of an XDG autostart <c>.desktop</c> file. Pure statics so the format is unit-tested without
/// touching a disk, the same split <c>ToolkitPaths</c> uses.
/// </summary>
internal static class DesktopEntry {
    /// <summary>The key that spells "ignore this entry". The spec's own way of disabling an autostart
    /// file without deleting it, which is what some desktop tools write instead of removing it.</summary>
    internal const string HiddenKey = "Hidden=true";

    /// <summary>
    /// The file to write for <paramref name="execPath"/>. <c>Exec</c> is quoted because the spec reserves
    /// space, and a path under <c>/home/My User/</c> would otherwise parse as two arguments.
    /// </summary>
    internal static string Build(string execPath) =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=DashDetective
        Comment=System monitoring dashboard
        Exec={QuoteExec(execPath)}
        Terminal=false
        X-GNOME-Autostart-enabled=true

        """;

    /// <summary>Whether an existing file counts as an enabled entry. A <c>Hidden=true</c> line means the
    /// spec says to ignore it, so reporting "enabled" for one would leave the toggle disagreeing with
    /// what actually happens at login.</summary>
    internal static bool IsEnabled(string content) =>
        !content.Contains(HiddenKey, StringComparison.OrdinalIgnoreCase);

    // Inside a quoted Exec value the spec reserves these four; each is escaped with a backslash. The
    // backslash goes first, or it would double the ones the others just added.
    private static string QuoteExec(string execPath) {
        var escaped = execPath
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }
}
