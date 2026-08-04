using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DashDetective.Tabs.Hardware.Catalog;

/// <summary>
/// A bundled, offline lookup for hardware facts that <b>no Windows API reports</b> — rated specs printed
/// on a part's datasheet (CPU boost/TDP, GPU CUDA-core count/memory type/bus, board form factor/M.2
/// count, RAM timings). These are fixed properties of a known model, so they are keyed here by the model
/// strings WMI already yields (CPU/GPU <c>Name</c>, board <c>Product</c>, memory <c>PartNumber</c>).
///
/// This is a pure enrichment layer: each per-card reader (<c>WindowsProcessorInfoProvider</c> and the
/// rest) calls it <i>after</i> its own WMI read and fills only the fields the machine couldn't report,
/// which is why the tables are keyed per domain. An unknown part returns <c>null</c> and
/// the caller leaves the neutral placeholder "—" — the catalog never guesses, and a near-miss must resolve
/// to nothing rather than to a similarly-named part's datasheet. No dependency, no admin, no network;
/// adding a part is a one-line entry in the relevant per-domain table.
/// </summary>
public static class HardwareCatalog {
    public static CpuSpec? LookupCpu(string name) => Match(CpuCatalog.Data, name);
    public static GpuSpec? LookupGpu(string name) => Match(GpuCatalog.Data, name);
    public static BoardSpec? LookupBoard(string product) => Match(BoardCatalog.Data, product);
    public static MemorySpec? LookupMemory(string partNumber) => Match(MemoryCatalog.Data, partNumber);

    /// <summary>
    /// Resolves a spec by normalizing the raw model string and matching it against the table keys — exact
    /// first, then a key that appears in the name as whole tokens (a short key like "7600X" inside the full
    /// WMI name). When several keys match, the <b>longest</b> wins, so a variant like "RTX 4070 TI" isn't
    /// shadowed by its base "RTX 4070". Returns <c>null</c> when nothing matches, which is the point: the
    /// caller then shows "—" rather than a wrong figure.
    ///
    /// Matching is deliberately one-directional and token-aligned. A key is never allowed to match because
    /// it <i>contains</i> the name, which would let a bare board product "B650" pick up a "B650E …" entry's
    /// form factor; and a key must align to token boundaries, so "RTX 4060" does not match inside a longer
    /// model token. <see cref="VariantMarkers"/> then rules out parts that merely share a desktop model's
    /// name.
    /// </summary>
    internal static TSpec? Match<TSpec>(IReadOnlyDictionary<string, TSpec> data, string raw)
        where TSpec : class {
        if (string.IsNullOrWhiteSpace(raw) || data.Count == 0)
            return null;

        var key = Normalize(raw);
        if (data.TryGetValue(key, out var exact))
            return exact;

        TSpec? best = null;
        var bestLen = 0;
        foreach (var pair in data) {
            if (pair.Key.Length > bestLen && ContainsTokens(key, pair.Key) && !VariantMismatch(key, pair.Key)) {
                best = pair.Value;
                bestLen = pair.Key.Length;
            }
        }

        return best;
    }

    /// <summary>Model-name tokens marking a physically different part that shares a desktop model's name — a
    /// mobile GPU has its own memory, clocks and core counts. Normalized, so "Max-Q" reads "MAX Q".</summary>
    private static readonly string[] VariantMarkers = { "LAPTOP", "MOBILE", "MAX Q" };

    /// <summary>True when the machine's part carries a variant marker the catalog key does not, e.g. a
    /// "GeForce RTX 4060 Laptop GPU" against the desktop "RTX 4060" entry. Such a part must fall through to
    /// "—" rather than inherit the desktop card's datasheet.</summary>
    private static bool VariantMismatch(string name, string catalogKey) {
        foreach (var marker in VariantMarkers)
            if (ContainsTokens(name, marker) && !ContainsTokens(catalogKey, marker))
                return true;
        return false;
    }

    /// <summary>True when <paramref name="haystack"/> contains <paramref name="needle"/> as whole
    /// space-separated tokens. <see cref="Normalize"/> collapses every separator run to a single space, so a
    /// boundary is the string edge or a space: "RTX 4060" is found in "GEFORCE RTX 4060 GPU" but not in
    /// "RTX 40600".</summary>
    internal static bool ContainsTokens(string haystack, string needle) {
        if (needle.Length == 0 || needle.Length > haystack.Length)
            return false;

        for (var from = 0; from <= haystack.Length - needle.Length;) {
            var index = haystack.IndexOf(needle, from, StringComparison.Ordinal);
            if (index < 0)
                return false;

            var end = index + needle.Length;
            if ((index == 0 || haystack[index - 1] == ' ') &&
                (end == haystack.Length || haystack[end] == ' '))
                return true;

            from = index + 1;
        }

        return false;
    }

    private static readonly Regex ClockSuffix = new(@"@\s*[\d.]+\s*GHZ", RegexOptions.Compiled);
    private static readonly Regex IgpuSuffix = new(@"WITH\s+.*GRAPHICS", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNum = new(@"[^A-Z0-9]+", RegexOptions.Compiled);

    /// <summary>Upper-cases and strips vendor cruft (trademarks, "CPU"/"Processor", the "@ x.xxGHz"
    /// suffix, an integrated-GPU suffix) so datasheet keys match WMI strings robustly. Table keys are
    /// stored already-normalized so callers can key on a short distinctive token.</summary>
    public static string Normalize(string raw) {
        var s = raw.ToUpperInvariant()
            .Replace("(R)", " ").Replace("(TM)", " ").Replace("®", " ").Replace("™", " ")
            .Replace(" CPU", " ").Replace("PROCESSOR", " ");
        s = ClockSuffix.Replace(s, " ");
        s = IgpuSuffix.Replace(s, " ");
        s = NonAlphaNum.Replace(s, " ");
        return s.Trim();
    }
}
