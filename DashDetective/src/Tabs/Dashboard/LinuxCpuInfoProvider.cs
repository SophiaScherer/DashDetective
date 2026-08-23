using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using DashDetective.Shared;
using System;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Reads static CPU hardware information from <c>/proc/cpuinfo</c> and <c>cpufreq</c>. The derivation is
/// shared with the Hardware tab's Processor card via <see cref="CpuFacts"/>, so both cards agree on the
/// core counts rather than each summing them their own way. The read is cheap — unlike the Windows arm's
/// ~100–300 ms WMI query — but still runs on a background thread so both arms honour the same async
/// contract. Any failure yields <see cref="CpuStaticInfo.Unknown"/> rather than throwing.
/// </summary>
internal sealed class LinuxCpuInfoProvider : ICpuInfoProvider {
    private readonly IProcFileSystem _proc;

    public LinuxCpuInfoProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so each source — and each source's absence — can be
    /// exercised against canned fixtures from any dev machine.</summary>
    internal LinuxCpuInfoProvider(IProcFileSystem proc) => _proc = proc;

    public Task<CpuStaticInfo> GetAsync() => Task.Run(Read);

    private CpuStaticInfo Read() {
        try {
            var facts = CpuFacts.Read(_proc);
            if (facts == CpuFacts.None)
                return CpuStaticInfo.Unknown;

            // Only the name is substituted here. The logical count needs no fallback: a readable cpuinfo
            // always has at least one block, and an unreadable one already returned Unknown above — which
            // carries the runtime's processor count itself. Physical cores and clock stay 0, rendering "—".
            return new CpuStaticInfo(
                string.IsNullOrWhiteSpace(facts.Name) ? Placeholders.UnknownProcessor : facts.Name,
                facts.PhysicalCores,
                facts.LogicalCores,
                facts.MaxClockMhz);
        } catch (Exception e) {
            Log.Warn("CpuInfoProvider read failed", e);
            return CpuStaticInfo.Unknown;
        }
    }
}
