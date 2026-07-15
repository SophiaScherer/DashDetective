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
/// This is a pure enrichment layer: <see cref="HardwareInfoProvider"/> calls it <i>after</i> its WMI
/// read and fills only the fields the machine couldn't report. An unknown part returns <c>null</c> and
/// the caller leaves the neutral placeholder "—" — the catalog never guesses. No dependency, no admin,
/// no network; adding a part is a one-line entry in the relevant per-domain table.
/// </summary>
public static class HardwareCatalog {
    public static CpuSpec? LookupCpu(string name) => Match(CpuCatalog.Data, name);
    public static GpuSpec? LookupGpu(string name) => Match(GpuCatalog.Data, name);
    public static BoardSpec? LookupBoard(string product) => Match(BoardCatalog.Data, product);
    public static MemorySpec? LookupMemory(string partNumber) => Match(MemoryCatalog.Data, partNumber);

    /// <summary>Resolves a spec by normalizing the raw model string and matching it against the table
    /// keys — exact first, then a substring match either way (a short key like "7600X" inside the full
    /// WMI name, or vice-versa). Returns <c>null</c> when nothing matches.</summary>
    private static TSpec? Match<TSpec>(IReadOnlyDictionary<string, TSpec> data, string raw)
        where TSpec : class {
        if (string.IsNullOrWhiteSpace(raw) || data.Count == 0)
            return null;

        var key = Normalize(raw);
        if (data.TryGetValue(key, out var exact))
            return exact;

        foreach (var pair in data) {
            if (key.Contains(pair.Key, StringComparison.Ordinal) ||
                pair.Key.Contains(key, StringComparison.Ordinal))
                return pair.Value;
        }

        return null;
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
