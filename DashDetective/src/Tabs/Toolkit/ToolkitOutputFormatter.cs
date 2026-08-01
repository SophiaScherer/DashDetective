using System;
using System.Globalization;
using System.Text;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Turns what a command produced into the line the Execution Log shows — pure statics, so every rule
/// here is testable without starting a process (the <c>ProcessFilter</c> / <c>ToolkitFilter</c>
/// pattern). Two jobs: shaping captured stream text, and wording the outcomes that have no output to
/// show.
///
/// The caps exist because the log is a 340px panel holding a whole session: <c>systeminfo</c> alone runs
/// to dozens of lines, and a runaway command could otherwise pin the UI thread laying out megabytes of
/// text. Truncation is announced rather than silent.
/// </summary>
public static class ToolkitOutputFormatter {
    /// <summary>Longest output kept, in lines.</summary>
    public const int MaxLines = 200;

    /// <summary>Longest output kept, in characters. Whichever cap bites first wins.</summary>
    public const int MaxCharacters = 16384;

    /// <summary>Appended when either cap trimmed the output.</summary>
    public const string TruncationMarker = "… output truncated";

    /// <summary>A command that finished cleanly but printed nothing (e.g. <c>ipconfig /flushdns</c>
    /// on some builds) — still a success, and the log must not look broken.</summary>
    public const string NoOutput = "Completed with no output.";

    /// <summary>A folder or location handed to Explorer.</summary>
    public const string Opened = "Opened.";

    /// <summary>A tool started in its own window.</summary>
    public const string Launched = "Launched.";

    /// <summary>An elevated launch. Windows refuses to redirect a <c>runas</c> process's streams, so
    /// the log says so rather than showing an empty body that reads as a failure.</summary>
    public const string LaunchedElevated = "Launched elevated — output not captured.";

    /// <summary>The user dismissed the UAC prompt. An expected outcome, not an error.</summary>
    public const string ElevationCancelled = "Cancelled at the elevation prompt.";

    /// <summary>
    /// Merges a command's two streams into one body, normalises line endings, drops trailing blank
    /// lines and applies the caps. stderr follows stdout because a command that writes to both is
    /// almost always reporting its trouble after its output.
    /// </summary>
    public static string Combine(string? standardOutput, string? standardError) {
        var merged = Join(Normalize(standardOutput), Normalize(standardError));
        return merged.Length == 0 ? "" : Cap(merged);
    }

    /// <summary>Applies the line and character caps, announcing the trim when either bites.</summary>
    public static string Cap(string text) {
        if (string.IsNullOrEmpty(text))
            return "";

        var capped = text;
        var truncated = false;

        var lines = capped.Split('\n');
        if (lines.Length > MaxLines) {
            capped = string.Join('\n', lines, 0, MaxLines);
            truncated = true;
        }

        if (capped.Length > MaxCharacters) {
            capped = capped[..MaxCharacters];
            truncated = true;
        }

        return truncated ? capped.TrimEnd() + '\n' + TruncationMarker : capped;
    }

    /// <summary>The body for a URL the runner refused. Names the target so a bad catalog entry is
    /// obvious rather than looking like a dead button.</summary>
    public static string BlockedUrl(string target) =>
        $"Refused: only https:// links are opened ({target}).";

    /// <summary>The body for a run the timeout killed, carrying whatever it managed to print.</summary>
    public static string TimedOut(TimeSpan timeout, string captured) {
        var seconds = ((int)timeout.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        var note = $"Timed out after {seconds}s — the command was stopped.";
        return captured.Length == 0 ? note : captured + "\n" + note;
    }

    /// <summary>The body for a command that ran but reported failure, carrying its output.</summary>
    public static string ExitedWith(int exitCode, string captured) {
        var note = $"Exited with code {exitCode.ToString(CultureInfo.InvariantCulture)}.";
        return captured.Length == 0 ? note : captured + "\n" + note;
    }

    /// <summary>The body for a launch the shell rejected — a missing tool, no association, access
    /// denied. The reason comes from the OS, so it stays useful without a lookup table here.</summary>
    public static string Failed(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "Could not run the command." : $"Could not run: {reason}";

    // CRLF/CR → LF so the console block wraps consistently, then trailing blank lines off: nearly every
    // Windows console tool signs off with one or two, which would otherwise pad every log stanza.
    private static string Normalize(string? text) {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var normalized = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (c == '\r') {
                // Swallow the LF of a CRLF pair; a lone CR still becomes one newline.
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                normalized.Append('\n');
            } else {
                normalized.Append(c);
            }
        }

        return normalized.ToString().Trim('\n', ' ', '\t');
    }

    private static string Join(string first, string second) =>
        (first.Length, second.Length) switch {
            (0, 0) => "",
            (0, _) => second,
            (_, 0) => first,
            _ => first + "\n" + second,
        };
}
