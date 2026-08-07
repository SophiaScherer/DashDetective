using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using DashDetective.Tabs.Hardware.Catalog;
using System;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Processor facts from <c>/proc/cpuinfo</c> and sysfs. Name, core and thread counts and the rated clock
/// come from <see cref="CpuFacts"/>, shared with the Dashboard's CPU tile so the two cards cannot disagree;
/// L3 cache comes from <c>cpu0</c>'s <c>cache/index*</c> entries. Boost clock and TDP have no source on
/// either platform, so they come from <see cref="HardwareCatalog"/> by model name and stay "—" when it has
/// no entry — exactly as on Windows, and formatted through the same
/// <see cref="ProcessorSpecFormatter"/> so the rows match.
///
/// <b>Socket designation is permanently "—" on Linux.</b> It lives in SMBIOS type 4, which the kernel does
/// not surface under <c>/sys/class/dmi/id</c>; only <c>dmidecode</c> reading <c>/dev/mem</c> as root can
/// see it.
/// </summary>
internal sealed class LinuxProcessorInfoProvider : IProcessorInfoProvider {
    private readonly IProcFileSystem _proc;

    public LinuxProcessorInfoProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so each source — and each source's absence — can be
    /// exercised against canned fixtures from any dev machine.</summary>
    internal LinuxProcessorInfoProvider(IProcFileSystem proc) => _proc = proc;

    public Task<ProcessorInfo> GetAsync() => Task.Run(Read);

    private ProcessorInfo Read() {
        try {
            var facts = CpuFacts.Read(_proc);
            if (facts == CpuFacts.None)
                return ProcessorInfo.Unknown;

            var spec = HardwareCatalog.LookupCpu(facts.Name);
            var threads = facts.LogicalCores > 0 ? facts.LogicalCores : Environment.ProcessorCount;

            return new ProcessorInfo(
                Name: string.IsNullOrEmpty(facts.Name) ? "—" : facts.Name,
                Cores: facts.PhysicalCores > 0 ? facts.PhysicalCores.ToString() : "—",
                LogicalProcessors: threads.ToString(),
                BaseBoost: ProcessorSpecFormatter.BaseBoost(facts.MaxClockMhz, spec?.Boost),
                CacheL3: ProcessorSpecFormatter.CacheL3(CpuFacts.L3CacheKilobytes(_proc)),
                Tdp: spec?.Tdp ?? "—",
                Socket: "—");
        } catch (Exception e) {
            Log.Warn("ProcessorInfoProvider read failed", e);
            return ProcessorInfo.Unknown;
        }
    }
}
