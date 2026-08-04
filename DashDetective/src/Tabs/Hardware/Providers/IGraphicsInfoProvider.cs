using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>Reads every physical graphics adapter for the Graphics cards. Implementations must never
/// throw: any failure yields <see cref="GraphicsInfo.Unknown"/>, which carries no adapters.</summary>
internal interface IGraphicsInfoProvider {
    Task<GraphicsInfo> GetAsync();
}
