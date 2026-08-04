using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>Reads the Motherboard card's facts — board, chipset, BIOS, form factor and slot counts.
/// Implementations must never throw: any failure yields <see cref="MotherboardInfo.Unknown"/>.</summary>
internal interface IMotherboardInfoProvider {
    Task<MotherboardInfo> GetAsync();
}
