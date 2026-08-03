using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>Reads the machine's static physical-memory facts. Implementations must never throw: any
/// failure yields <see cref="MemoryStaticInfo.Unknown"/>.</summary>
internal interface IMemoryInfoProvider {
    Task<MemoryStaticInfo> GetAsync();
}
