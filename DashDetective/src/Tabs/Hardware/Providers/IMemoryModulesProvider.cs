using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// Reads the Memory card's facts from the installed DIMMs — deliberately not
/// <c>Tabs.Dashboard.IMemoryInfoProvider</c>, which reads total installed RAM for a live gauge. This one
/// enumerates the modules: per-stick capacity, speed, type, voltage and how many slots they fill.
/// Implementations must never throw: any failure yields <see cref="MemoryInfo.Unknown"/>.
/// </summary>
internal interface IMemoryModulesProvider {
    Task<MemoryInfo> GetAsync();
}
