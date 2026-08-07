using System;
using System.Collections.Generic;
using System.Globalization;

namespace DashDetective.Services.Platform.Linux;

/// <summary>
/// The processor facts <c>/proc/cpuinfo</c> and <c>cpufreq</c> can supply, derived once and shared by the
/// two cards that need them: the Dashboard's CPU tile and the Hardware tab's Processor card. The
/// derivation lives here rather than in either provider because both want the same four numbers and only
/// the presentation differs — which is the mistake the WMI arm avoids by summing sockets in one place.
///
/// <b>Every field reports "not known" honestly</b> — "" or 0 — rather than substituting a plausible
/// stand-in. Each consumer applies its own placeholder, because they differ ("Unknown processor" on the
/// Dashboard, "—" on the Hardware card) and because a substitution made here would be invisible to both.
/// </summary>
internal sealed record CpuFacts(string Name, int PhysicalCores, int LogicalCores, double MaxClockMhz) {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string CpuInfoPath = "/proc/cpuinfo";
    private const string CpuRoot = "/sys/devices/system/cpu";
    private const string CpuPrefix = "cpu";

    /// <summary>Nothing readable — an empty <c>/proc</c>, or an architecture whose <c>cpuinfo</c> carries
    /// none of these keys.</summary>
    internal static CpuFacts None { get; } = new("", 0, 0, 0);

    /// <summary>Reads and derives the facts. Never throws: an unreadable source yields
    /// <see cref="None"/>.</summary>
    internal static CpuFacts Read(IProcFileSystem proc) {
        var blocks = ProcCpuinfoParser.Parse(proc.ReadAllLines(CpuInfoPath));
        if (blocks.Count == 0)
            return None;

        var name = ProcCpuinfoParser.Value(blocks[0], "model name");

        return new CpuFacts(name, PhysicalCoresOf(blocks), blocks.Count, MaxClockMhzOf(proc, name));
    }

    /// <summary>
    /// The physical core count, from the distinct <c>(physical id, core id)</c> pairs — the only reading
    /// that is right on both a hyperthreaded chip (two blocks share a pair) and a multi-socket board (the
    /// same core id recurs under a different physical id). Falls back to <c>cpu cores</c> multiplied by
    /// the socket count for kernels that omit <c>core id</c>, then to 0: ARM and many virtualised
    /// <c>cpuinfo</c>s carry none of these keys, and 0 renders "—" rather than a guess.
    /// </summary>
    private static int PhysicalCoresOf(IReadOnlyList<IReadOnlyDictionary<string, string>> blocks) {
        var cores = new HashSet<(string Package, string Core)>();
        var packages = new HashSet<string>(StringComparer.Ordinal);

        foreach (var block in blocks) {
            var package = ProcCpuinfoParser.Value(block, "physical id");
            var core = ProcCpuinfoParser.Value(block, "core id");

            if (package.Length > 0)
                packages.Add(package);
            if (package.Length > 0 && core.Length > 0)
                cores.Add((package, core));
        }

        if (cores.Count > 0)
            return cores.Count;

        var perPackage = ParseInt(ProcCpuinfoParser.Value(blocks[0], "cpu cores"));
        return perPackage > 0 ? perPackage * Math.Max(packages.Count, 1) : 0;
    }

    /// <summary>
    /// The rated maximum clock in MHz. Prefers <c>cpufreq</c>'s <c>cpuinfo_max_freq</c> (kHz), taking the
    /// highest across the online cores so a heterogeneous chip reports its fast cores rather than whichever
    /// one happens to be <c>cpu0</c>. Falls back to the clock in the model name
    /// ("… CPU @ 3.60GHz"), which is the rated base clock and survives the VMs where <c>cpufreq</c> is
    /// absent entirely.
    ///
    /// <b><c>cpu MHz</c> is deliberately not consulted.</b> It is the core's clock at the instant of the
    /// read, so under a scaling governor it reports an idle 800 MHz under a "max" label — the near-miss
    /// this port refuses to substitute. The live clock has its own reader in
    /// <c>LinuxProcessorFrequencySampler</c>.
    /// </summary>
    private static double MaxClockMhzOf(IProcFileSystem proc, string modelName) {
        double kHz = 0;
        foreach (var entry in proc.ListDirectory(CpuRoot)) {
            if (!IsCpuDirectory(entry))
                continue;

            var text = proc.ReadAllText(CpuRoot + "/" + entry + "/cpufreq/cpuinfo_max_freq");
            if (text is not null
                && double.TryParse(
                    text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                kHz = Math.Max(kHz, value);
        }

        return kHz > 0 ? kHz / 1000.0 : ModelNameMhz(modelName);
    }

    /// <summary>Whether a <c>/sys/devices/system/cpu</c> entry is a per-core directory (<c>cpu0</c>,
    /// <c>cpu11</c>) rather than one of its many siblings (<c>cpufreq</c>, <c>online</c>,
    /// <c>possible</c>).</summary>
    private static bool IsCpuDirectory(string entry) =>
        entry.StartsWith(CpuPrefix, StringComparison.Ordinal)
        && entry.Length > CpuPrefix.Length
        && int.TryParse(
            entry.AsSpan(CpuPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out _);

    /// <summary>The clock Intel and some AMD parts append to the model name, e.g.
    /// "Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz" → 3600. Returns 0 for the many names that carry none
    /// ("AMD Ryzen 9 5900X 12-Core Processor").</summary>
    private static double ModelNameMhz(string modelName) {
        var at = modelName.LastIndexOf('@');
        if (at < 0)
            return 0;

        var rest = modelName.AsSpan(at + 1).Trim();

        // Split the number from its unit: the first character that is neither a digit nor the decimal point.
        var end = 0;
        while (end < rest.Length && (char.IsAsciiDigit(rest[end]) || rest[end] == '.'))
            end++;

        if (end == 0
            || !double.TryParse(
                rest[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return 0;

        var unit = rest[end..].Trim();
        if (unit.Equals("GHz", StringComparison.OrdinalIgnoreCase))
            return value * 1000;

        return unit.Equals("MHz", StringComparison.OrdinalIgnoreCase) ? value : 0;
    }

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
