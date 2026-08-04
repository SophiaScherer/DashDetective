using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>Reads the machine's static CPU facts. Implementations must never throw: any failure
/// yields <see cref="CpuStaticInfo.Unknown"/>.</summary>
internal interface ICpuInfoProvider {
    Task<CpuStaticInfo> GetAsync();
}
