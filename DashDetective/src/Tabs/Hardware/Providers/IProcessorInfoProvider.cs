using System.Threading.Tasks;

namespace DashDetective.Tabs.Hardware;

/// <summary>Reads the Processor card's facts. Implementations must never throw: any failure yields
/// <see cref="ProcessorInfo.Unknown"/>, so every row renders "—".</summary>
internal interface IProcessorInfoProvider {
    Task<ProcessorInfo> GetAsync();
}
