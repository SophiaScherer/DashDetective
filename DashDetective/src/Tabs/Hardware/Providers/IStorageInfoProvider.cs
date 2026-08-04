using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>Reads the Storage Devices card's facts, one row per physical disk. Implementations must
/// never throw: any failure yields <see cref="StorageInfo.Unknown"/>, which carries no drives.</summary>
internal interface IStorageInfoProvider {
    Task<StorageInfo> GetAsync();
}
