using System;
using System.Collections.Generic;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Reads the cgroup v2 path out of <c>/proc/[pid]/cgroup</c>. Every line is
/// <c>hierarchy-ID:controller-list:path</c>; the unified v2 hierarchy is the one with ID <c>0</c> and an
/// <b>empty</b> controller list, which is why a reader cannot simply take the first or last line — a
/// hybrid v1/v2 host lists a dozen v1 controllers alongside it.
///
/// This is what <c>LinuxProcessClassifier</c> classifies on: it is world-readable, needs no display server
/// and no root, and on any systemd distro it encodes the App/Background/System split directly.
/// </summary>
internal static class ProcCgroupParser {
    /// <summary>The unified hierarchy's path, or <c>""</c> on a v1-only host (or an unreadable file) — the
    /// caller reads empty as "cannot tell" and falls back.</summary>
    internal static string Parse(IReadOnlyList<string> lines) {
        foreach (var line in lines) {
            var first = line.IndexOf(':');
            if (first < 0)
                continue;

            var second = line.IndexOf(':', first + 1);
            if (second < 0)
                continue;

            if (!line.AsSpan(0, first).Trim().Equals("0", StringComparison.Ordinal))
                continue;

            // A non-empty controller list here means a v1 hierarchy that merely happens to be numbered 0.
            if (!line.AsSpan(first + 1, second - first - 1).Trim().IsEmpty)
                continue;

            return line[(second + 1)..].Trim();
        }

        return "";
    }

    /// <summary>The path's last segment — <c>app-gnome-firefox-3456.scope</c>, <c>cron.service</c>. Two of
    /// the classifier's rules match on the leaf rather than the whole path, so the split lives here beside
    /// the format knowledge rather than in the classifier.</summary>
    internal static string Leaf(string path) {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }
}
