using System;
using System.IO;

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
    /// startup.</summary>
    public static string Resolve(string target) => Environment.ExpandEnvironmentVariables(target);

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
}
