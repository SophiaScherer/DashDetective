using DashDetective.Shared;

namespace DashDetective.Tabs.Storage;

/// <summary>
/// The Storage tab: a read-only drives/health view per the design comp — three drive summary cards over
/// a Partitions table and a Disk Activity chart. Page-scrolls as a whole like the Dashboard/Network (not
/// <see cref="ISelfScrollingPage"/>).
///
/// This is the initial UI pass: surfaces are seeded with <b>static mock data</b>. Live samplers/providers
/// (<c>StorageUsageSampler</c>, <c>DiskInfoProvider</c> in <c>src/Services/SystemMetrics</c>) and the
/// <see cref="IRefreshablePage"/> / <see cref="ILiveSamplingPage"/> seams are a later technical pass.
/// </summary>
public partial class StorageViewModel : ViewModelBase {
}
