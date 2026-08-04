using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>Reads the machine's static identity — OS, device, BIOS, board, build. Implementations must
/// never throw: each section falls back independently, so one dead source can't blank the panel.</summary>
internal interface ISystemInfoProvider {
    Task<SystemStaticInfo> GetAsync();
}
