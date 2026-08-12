using DashDetective.Services.Platform.Linux;
using DashDetective.Services.SystemMetrics;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="LinuxDiskTemperatureProvider"/>: the two different hwmon-to-block-device
/// walks, matching on the sensor's name rather than its index, and the millidegree scale.</summary>
public class LinuxDiskTemperatureProviderTests {
    // The packed major:minor SysBlockFacts derives, which is what PhysicalDiskInfo is keyed by.
    private static readonly int Sda = SysBlockFacts.Pack(8, 0);
    private static readonly int Nvme0n1 = SysBlockFacts.Pack(259, 0);

    private static double? Read(FakeProcFileSystem proc, int deviceId) =>
        new LinuxDiskTemperatureProvider(proc).ReadCelsius(deviceId);

    /// <summary>NVMe is the two-hop walk: the hwmon's device is the controller, and the block device is a
    /// namespace child of it. Treating the link target as the device finds nothing here.</summary>
    [Fact]
    public void ReadCelsius_Nvme_ResolvesThroughTheControllerToItsNamespace() {
        var proc = new FakeProcFileSystem()
            .WithNvmeHwmon()
            .WithFile("/sys/block/nvme0n1/dev", "259:0\n");

        Assert.Equal(42.85, Read(proc, Nvme0n1));
    }

    /// <summary>drivetemp is the other shape: a SCSI target with the block device under
    /// <c>block/</c>.</summary>
    [Fact]
    public void ReadCelsius_Drivetemp_ResolvesThroughTheScsiTargetsBlockChild() {
        var proc = new FakeProcFileSystem().WithDrivetempHwmon().WithVirtualBoxBlockTree();

        Assert.Equal(38, Read(proc, Sda));
    }

    /// <summary>
    /// hwmon numbering is not stable across boots, and the low numbers are usually the CPU package and the
    /// ACPI thermal zone. A reader that indexes instead of matching on <c>name</c> reports the processor's
    /// temperature on a drive card — here it would claim the drive is at 45 °C.
    /// </summary>
    [Fact]
    public void ReadCelsius_IgnoresNonDriveSensorsHoweverTheyAreNumbered() {
        var proc = new FakeProcFileSystem()
            .WithNonDriveHwmon()
            .WithDrivetempHwmon()
            .WithVirtualBoxBlockTree();

        Assert.Equal(38, Read(proc, Sda));
    }

    /// <summary>A machine with both kinds must attribute each reading to its own drive.</summary>
    [Fact]
    public void ReadCelsius_BothSensorKinds_AttributesEachToItsOwnDisk() {
        var proc = new FakeProcFileSystem()
            .WithNonDriveHwmon()
            .WithNvmeHwmon()
            .WithDrivetempHwmon()
            .WithVirtualBoxBlockTree()
            .WithFile("/sys/block/nvme0n1/dev", "259:0\n");

        Assert.Equal(38, Read(proc, Sda));
        Assert.Equal(42.85, Read(proc, Nvme0n1));
    }

    /// <summary>The expected outcome on most real machines: NVMe registers a hwmon automatically, SATA only
    /// through <c>drivetemp</c>, which most distributions do not load.</summary>
    [Fact]
    public void ReadCelsius_NoDriveSensor_IsNull() {
        var proc = new FakeProcFileSystem().WithNonDriveHwmon().WithVirtualBoxBlockTree();

        Assert.Null(Read(proc, Sda));
    }

    [Fact]
    public void ReadCelsius_UnknownDisk_IsNull() {
        var proc = new FakeProcFileSystem().WithDrivetempHwmon().WithVirtualBoxBlockTree();

        Assert.Null(Read(proc, SysBlockFacts.Pack(8, 16)));
    }

    [Fact]
    public void ReadCelsius_NoHwmonAtAll_IsNull() {
        Assert.Null(Read(new FakeProcFileSystem(), Sda));
    }

    /// <summary>42850 millidegrees is 42.85 °C; read as whole degrees it would report a drive at forty-two
    /// thousand.</summary>
    [Theory]
    [InlineData("42850\n", 42.85)]
    [InlineData("38000\n", 38.0)]
    [InlineData("0\n", 0.0)]
    public void ParseMillidegrees_DividesByAThousand(string text, double expected) {
        Assert.Equal(expected, LinuxDiskTemperatureProvider.ParseMillidegrees(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("N/A\n")]
    public void ParseMillidegrees_UnreadableIsNull(string? text) {
        Assert.Null(LinuxDiskTemperatureProvider.ParseMillidegrees(text));
    }

    /// <summary>A sensor reporting 0 means "not reported", not a drive at freezing.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(200.0)]
    [InlineData(null)]
    public void PlausibleCelsius_RejectsImpossibleReadings(double? celsius) {
        Assert.Null(LinuxDiskTemperatureProvider.PlausibleCelsius(celsius));
    }

    [Theory]
    [InlineData("nvme\n", true)]
    [InlineData("drivetemp\n", true)]
    [InlineData("coretemp\n", false)]
    [InlineData("acpitz\n", false)]
    [InlineData("amdgpu\n", false)]
    [InlineData(null, false)]
    public void IsDriveSensor_NamesTheTwoDriveSensorsOnly(string? name, bool expected) {
        Assert.Equal(expected, LinuxDiskTemperatureProvider.IsDriveSensor(name));
    }

    /// <summary>The real constructor reads the live filesystem; on a box with no <c>/sys</c> that must be
    /// null rather than a throw.</summary>
    [Fact]
    public void RealFileSystem_SoftFailsToNull() {
        if (System.OperatingSystem.IsLinux())
            return;

        Assert.Null(new LinuxDiskTemperatureProvider().ReadCelsius(Sda));
    }
}
