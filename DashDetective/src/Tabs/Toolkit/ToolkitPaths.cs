using System;
using System.IO;
using System.Text;

namespace DashDetective.Tabs.Toolkit;

/// <summary>
/// Where a path-shaped target actually points, once the environment has had its say. Pure statics (like
/// <see cref="ToolkitHostValidator"/>) so the two things that need the answer agree on it: the runner,
/// which hands the target to the shell, and the rows, which offer to open it in the app's own File
/// Explorer instead.
/// </summary>
public static class ToolkitPaths {
    // A shell location ("shell:startup") is named by the shell namespace rather than the filesystem, so
    // it resolves for Explorer but there is no path for the in-app File Explorer to navigate to.
    private const string ShellPrefix = "shell:";

    /// <summary>Expands environment variables at call time rather than when the catalog was built, so
    /// <c>%temp%</c> and friends follow the session instead of baking in whatever they meant at
    /// startup. Each platform gets its own notation: <c>%VAR%</c> on Windows, <c>$VAR</c>,
    /// <c>${VAR}</c> and a leading <c>~</c> elsewhere.</summary>
    public static string Resolve(string target) => Expand(target, OperatingSystem.IsWindows());

    /// <summary>Test seam: takes the platform explicitly, so both notations are exercised from either
    /// dev machine. <see cref="Resolve"/> passes <c>OperatingSystem.IsWindows()</c>.</summary>
    internal static string Expand(string target, bool windows) {
        if (string.IsNullOrEmpty(target))
            return target;

        // ExpandEnvironmentVariables only understands %VAR%, so off Windows it is an identity function.
        return windows ? Environment.ExpandEnvironmentVariables(target) : ExpandUnix(target);
    }

    /// <summary>
    /// Whether the target names a place on disk the in-app File Explorer could open — as opposed to a
    /// <c>shell:</c> location, or a bare command resolved off the PATH. Judged on the *resolved* value,
    /// so <c>%appdata%</c> counts and <c>regedit</c> does not.
    /// </summary>
    public static bool IsFileSystemPath(string? target) {
        if (string.IsNullOrWhiteSpace(target) ||
            target.StartsWith(ShellPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return Path.IsPathRooted(Resolve(target));
    }

    // The home shorthand is split off the front before variables are expanded, so an expanded value that
    // happens to contain "~" or "$" is never re-expanded.
    private static string ExpandUnix(string target) {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var shorthand = home.Length > 0 && StartsWithHome(target);

        return shorthand ? home + ExpandVariables(target[1..]) : ExpandVariables(target);
    }

    // "~" or "~/…" only. "~other" names another user's home, which needs a passwd lookup, so it is
    // left literal rather than silently resolved to the wrong person's folder.
    private static bool StartsWithHome(string target) =>
        target[0] == '~' && (target.Length == 1 || target[1] == '/');

    private static string ExpandVariables(string target) {
        if (!target.Contains('$', StringComparison.Ordinal))
            return target;

        var result = new StringBuilder(target.Length);
        for (var i = 0; i < target.Length; i++) {
            if (target[i] != '$') {
                result.Append(target[i]);
                continue;
            }

            var braced = i + 1 < target.Length && target[i + 1] == '{';
            var start = braced ? i + 2 : i + 1;
            var end = start;
            while (end < target.Length && IsNameChar(target[end], end == start))
                end++;

            // A bare "$", or "${NAME" with no closing brace, is not a reference — keep it as typed.
            if (end == start || (braced && (end == target.Length || target[end] != '}'))) {
                result.Append(target[i]);
                continue;
            }

            // An unset variable stays literal, matching how %NOPE% survives Windows expansion.
            var reference = target[i..(braced ? end + 1 : end)];
            result.Append(Environment.GetEnvironmentVariable(target[start..end]) ?? reference);
            i = braced ? end : end - 1;
        }

        return result.ToString();
    }

    private static bool IsNameChar(char c, bool first) =>
        c == '_' || char.IsAsciiLetter(c) || (!first && char.IsAsciiDigit(c));
}
