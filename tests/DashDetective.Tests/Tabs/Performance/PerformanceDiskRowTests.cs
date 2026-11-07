using DashDetective.Services.Network;
using DashDetective.Services.SystemMetrics;
using DashDetective.Shared;
using DashDetective.Tabs.Performance;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Pins that a disk's rail row and tiles say "—" until the sampler reports, and keep saying it if it never does; a real 0 % still shows.</summary>
public class PerformanceDiskRowTests {
    private static readonly PhysicalDiskInfo Boot = new(3, "Boot Drive", "NVMe SSD", 1_000_000_000_000, true);

    private static IReadOnlyList<VolumeInfo> Volumes() => [
        new(Boot.DeviceId, SystemDrive.Letter, "", "NTFS", 1_000_000_000_000, 500_000_000_000),
    ];

    private static async Task<ResourceRow> DiskRowAsync(FakeDiskThroughputSampler throughput) {
        var samplers = new MetricSamplers(
            () => 0, () => new MemorySample(0, 0, 0, 0, 0), () => new NetworkSample(0, 0), () => "TestNIC");
        var viewModel = new PerformanceViewModel(
            new SystemMetricsService(samplers, () => new FakeUiTimer()),
            StubHardwareProviders.With(disks: [Boot], volumes: Volumes()),
            () => new FakeGpuUsageSampler(), throughputSampler: throughput);

        await viewModel.InitialLoad;
        await viewModel.LoadInventoryAsync();
        return viewModel.Resources.Single(r => r.DeviceId == DeviceIds.Disk(Boot.DeviceId));
    }

    private static string Tile(ResourceRow row, string label) => row.Stats.Single(t => t.Label == label).Value;

    [Fact]
    public async Task LoadInventoryAsync_SamplerReportsNothing_ShowsThePlaceholder() {
        var row = await DiskRowAsync(new FakeDiskThroughputSampler());

        Assert.Equal(Placeholders.NoReading, row.ValueText);
        Assert.Equal("", row.Unit);
        Assert.All(new[] { "Active", "Read", "Write", "Response" },
                   label => Assert.Equal(Placeholders.NoReading, Tile(row, label)));
    }

    [Fact]
    public async Task LoadInventoryAsync_DiskReportingZero_ShowsZero() {
        var row = await DiskRowAsync(new FakeDiskThroughputSampler().Reporting(Boot.DeviceId, 0));

        Assert.Equal("0", row.ValueText);
        Assert.Equal("%", row.Unit);
        Assert.Equal("0 %", Tile(row, "Active"));
        Assert.Equal(ByteRateFormatter.Format(0), Tile(row, "Read"));
        Assert.Equal("0.0 ms", Tile(row, "Response"));
    }
}
