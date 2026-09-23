using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// Parses <c>/etc/group</c>: one group per line as <c>name:password:gid:members</c>. Only the name and the
/// gid are read; the member list omits each user's primary group, so membership is taken from the
/// process's own groups instead.
/// </summary>
internal static class EtcGroupParser {
    /// <summary>The gids of the groups whose names are in <paramref name="names"/>. A comment, a short line
    /// or an unparseable gid is skipped, not fatal.</summary>
    internal static IReadOnlySet<int> GidsNamed(IReadOnlyList<string> lines, IReadOnlySet<string> names) {
        var gids = new HashSet<int>();
        foreach (var line in lines) {
            if (line.StartsWith('#'))
                continue;

            var fields = line.Split(':');
            if (fields.Length < 3 || !names.Contains(fields[0]))
                continue;

            if (int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gid))
                gids.Add(gid);
        }

        return gids;
    }
}
