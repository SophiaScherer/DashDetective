using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxPhysicalDiskProvider"/>: the drive cards it composes, and the two facts
/// it refuses to invent without root.</summary>
public class LinuxPhysicalDiskProviderTests {
    /// <summary>Records which disks were asked for a temperature, so the "NVMe only" rule can be pinned
    /// without a real drive.</summary>
    private sealed class SpyTemperatureProvider(double? celsius = null) : IDiskTemperatureProvider {
        public List<int> Reads { get; } = [];

        public double? ReadCelsius(int deviceId) {
            Reads.Add(deviceId);
            return celsius;
        }
    }

    private static Task<IReadOnlyList<PhysicalDiskInfo>> ReadAsync(
        FakeProcFileSystem proc, IDiskTemperatureProvider? temperature = null) =>
        new LinuxPhysicalDiskProvider(
            temperature ?? new UnsupportedDiskTemperatureProvider(), proc).GetAsync();

    private static FakeProcFileSystem VirtualBox() =>
        new FakeProcFileSystem().WithVirtualBoxBlockTree();

    private static FakeProcFileSystem Nvme() =>
        new FakeProcFileSystem()
            .WithFile("/sys/block/nvme0n1/dev", "259:0\n")
            .WithFile("/sys/block/nvme0n1/size", "3907029168\n")
            .WithFile("/sys/block/nvme0n1/queue/rotational", "0\n")
            .WithFile("/sys/block/nvme0n1/device/model", "Samsung SSD 980 PRO 2TB\n");

    /// <summary>The milestone's acceptance criterion at the surface a user sees: one card, no loop
    /// devices.</summary>
    [Fact]
    public async Task GetAsync_RendersOneCardPerRealDisk() {
        var disk = Assert.Single(await ReadAsync(VirtualBox()));

        Assert.Equal("VBOX HARDDISK", disk.Model);
        Assert.Equal(41943040UL * 512, disk.SizeBytes);
    }

    /// <summary>The Storage tab's spelled-out wording, shared with the Windows arm so both platforms label
    /// the same drive identically.</summary>
    [Theory]
    [InlineData("1", "HDD")]
    [InlineData("0", "SSD")]
    public async Task GetAsync_LabelsTheDriveFromItsRotationalFlag(string rotational, string expected) {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/block/sda/dev", "8:0\n")
            .WithFile("/sys/block/sda/size", "1024\n")
            .WithFile("/sys/block/sda/queue/rotational", rotational + "\n");

        Assert.Equal(expected, (await ReadAsync(proc)).Single().TypeLabel);
    }

    [Fact]
    public async Task GetAsync_LabelsAnNvmeDrive() =>
        Assert.Equal("NVMe SSD", (await ReadAsync(Nvme())).Single().TypeLabel);

    /// <summary>A device that reports no model at all still gets a card, under the same placeholder the
    /// Windows arm uses.</summary>
    [Fact]
    public async Task GetAsync_WithNoModel_NamesTheCardDrive() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/block/vda/dev", "253:0\n")
            .WithFile("/sys/block/vda/size", "1024\n");

        Assert.Equal("Drive", (await ReadAsync(proc)).Single().Model);
    }

    /// <summary>SMART needs root, so health is reported as healthy rather than as a warning the user
    /// cannot act on. The card's Caution state stays reserved for a real reading.</summary>
    [Fact]
    public async Task GetAsync_ReportsHealthyWithoutSmart() =>
        Assert.True((await ReadAsync(VirtualBox())).Single().IsHealthy);

    /// <summary>The temperature provider is wired in exactly as the Windows arm wires it, so the milestone
    /// that lands a real one is a single swap — and only NVMe drives are asked, matching that arm.</summary>
    [Fact]
    public async Task GetAsync_AsksForATemperatureOnlyForNvmeDrives() {
        var spy = new SpyTemperatureProvider();
        await ReadAsync(VirtualBox(), spy);

        Assert.Empty(spy.Reads);
    }

    [Fact]
    public async Task GetAsync_StampsAnNvmeCardWithTheReportedTemperature() {
        var spy = new SpyTemperatureProvider(41.5);

        Assert.Equal(41.5, (await ReadAsync(Nvme(), spy)).Single().TemperatureCelsius);
    }

    /// <summary>Until the temperature milestone lands, the unsupported reader means every card shows "—"
    /// rather than a wrong number.</summary>
    [Fact]
    public async Task GetAsync_WithNoTemperatureReader_ReportsNoTemperature() =>
        Assert.Null((await ReadAsync(Nvme())).Single().TemperatureCelsius);

    [Fact]
    public async Task GetAsync_WithNoSysBlock_ReportsNoDisks() =>
        Assert.Empty(await ReadAsync(new FakeProcFileSystem()));
}
