using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins the <see cref="HardwareProviders"/> contract: which set the platform resolves to, that
/// the unsupported set returns exactly the <c>.Unknown</c> / empty / <c>null</c> values the old inline
/// <c>OperatingSystem.IsWindows()</c> guards returned, and that the temperature reader is shared rather
/// than duplicated.
///
/// The fallback set is the thing most likely to rot: nothing on a Windows dev machine or a Windows CI
/// runner ever executes it, so only this test notices if a member starts returning something else.</summary>
public class HardwareProvidersTests {
    [Fact]
    public void ForCurrentPlatform_ResolvesTheSetForThisHost() {
        var providers = HardwareProviders.ForCurrentPlatform();

        if (OperatingSystem.IsWindows()) {
            Assert.IsType<WindowsCpuInfoProvider>(providers.Cpu);
            Assert.IsType<WindowsMemoryInfoProvider>(providers.Memory);
            Assert.IsType<WindowsSystemInfoProvider>(providers.System);
            Assert.IsType<WindowsGpuAdapterProvider>(providers.GpuAdapters);
            Assert.IsType<WindowsPhysicalDiskProvider>(providers.Disks);
            Assert.IsType<WindowsVolumeProvider>(providers.Volumes);
            Assert.IsType<WindowsDiskTemperatureProvider>(providers.DiskTemperature);
        } else if (OperatingSystem.IsLinux()) {
            // The port fills this set in one milestone at a time; the members still on Unsupported* are
            // the ones whose milestone has not landed, and each moves here when it does.
            Assert.IsType<LinuxCpuInfoProvider>(providers.Cpu);
            Assert.IsType<LinuxSystemInfoProvider>(providers.System);
            Assert.IsType<UnsupportedMemoryInfoProvider>(providers.Memory);
            Assert.IsType<UnsupportedGpuAdapterProvider>(providers.GpuAdapters);
            Assert.IsType<UnsupportedPhysicalDiskProvider>(providers.Disks);
            Assert.IsType<UnsupportedVolumeProvider>(providers.Volumes);
            Assert.IsType<UnsupportedDiskTemperatureProvider>(providers.DiskTemperature);
        } else {
            Assert.IsType<UnsupportedCpuInfoProvider>(providers.Cpu);
            Assert.IsType<UnsupportedMemoryInfoProvider>(providers.Memory);
            Assert.IsType<UnsupportedSystemInfoProvider>(providers.System);
            Assert.IsType<UnsupportedGpuAdapterProvider>(providers.GpuAdapters);
            Assert.IsType<UnsupportedPhysicalDiskProvider>(providers.Disks);
            Assert.IsType<UnsupportedVolumeProvider>(providers.Volumes);
            Assert.IsType<UnsupportedDiskTemperatureProvider>(providers.DiskTemperature);
        }
    }

    /// <summary>Each fallback returns the same "nothing to report" value its old guard did, so an
    /// unsupported host renders "—" and empty tables rather than blanking or throwing.</summary>
    [Fact]
    public async Task Unsupported_ReturnsTheSameNothingToReportValuesTheOldGuardsDid() {
        Assert.Same(CpuStaticInfo.Unknown, await new UnsupportedCpuInfoProvider().GetAsync());
        Assert.Same(MemoryStaticInfo.Unknown, await new UnsupportedMemoryInfoProvider().GetAsync());
        Assert.Same(SystemStaticInfo.Unknown, await new UnsupportedSystemInfoProvider().GetAsync());
        Assert.Empty(await new UnsupportedGpuAdapterProvider().GetAsync());
        Assert.Empty(await new UnsupportedPhysicalDiskProvider().GetAsync());
        Assert.Empty(await new UnsupportedVolumeProvider().GetAsync());
        Assert.Null(new UnsupportedDiskTemperatureProvider().ReadCelsius(0));
    }

    /// <summary>The Storage page and the disk enumeration must read temperature through one object, so
    /// "the drive's temperature" has a single source. Cosmetic while the reader is stateless — which is
    /// exactly why it would go unnoticed if someone later added a cache.</summary>
    [Fact]
    public void Windows_SharesOneTemperatureReaderWithTheDiskEnumeration() {
        if (!OperatingSystem.IsWindows())
            return;

        var providers = HardwareProviders.ForCurrentPlatform();

        Assert.Same(providers.DiskTemperature, DiskTemperatureReaderOf(providers.Disks));
    }

    /// <summary>Reads back the temperature reader a <see cref="WindowsPhysicalDiskProvider"/> was built
    /// with — it is a primary-constructor parameter, so it is captured as a private field.</summary>
    private static object? DiskTemperatureReaderOf(IPhysicalDiskProvider disks) {
        foreach (var field in disks.GetType()
                     .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            if (typeof(IDiskTemperatureProvider).IsAssignableFrom(field.FieldType))
                return field.GetValue(disks);
        return null;
    }
}
