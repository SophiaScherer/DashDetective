using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Performance;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Performance;

/// <summary>Covers <see cref="WindowsGpuSensorProvider"/>: routing an adapter to the reader for its PCI
/// vendor, reporting nothing for a vendor with no reader (how the AMD/Intel tiles stay "—"), and containing
/// a reader that throws rather than letting it reach the sampling tick. Every case goes through the
/// unannotated test-seam constructor, so all of it runs on the Linux leg too.</summary>
public class WindowsGpuSensorProviderTests {
    private static readonly GpuPciId Nvidia = new(0x10DE, 0x2504, 0x397D1462, 0xA1);
    private static readonly GpuPciId Amd = new(0x1002, 0x164E, 0x7D731462, 0xC7);

    [Fact]
    public void SelectReader_MatchingVendor_ReturnsThatReader() {
        var nvidia = new FakeReader(0x10DE);
        var readers = new List<IGpuSensorReader> { new FakeReader(0x1002), nvidia };
        Assert.Same(nvidia, WindowsGpuSensorProvider.SelectReader(0x10DE, readers));
    }

    [Fact]
    public void SelectReader_NoReaderForVendor_ReturnsNull() {
        var readers = new List<IGpuSensorReader> { new FakeReader(0x10DE) };
        Assert.Null(WindowsGpuSensorProvider.SelectReader(0x1002, readers));
    }

    [Fact]
    public void SelectReader_NoReadersAtAll_ReturnsNull() {
        Assert.Null(WindowsGpuSensorProvider.SelectReader(0x10DE, new List<IGpuSensorReader>()));
    }

    [Fact]
    public void Read_VendorWithAReader_ReturnsItsSample() {
        using var provider = new WindowsGpuSensorProvider(new[] { new FakeReader(0x10DE, new GpuSensorSample(41, 16.42)) });

        var sample = provider.Read("gpu-a", Nvidia);

        Assert.Equal(41, sample.TemperatureCelsius);
        Assert.Equal(16.42, sample.PowerWatts);
    }

    /// <summary>An adapter whose vendor has no reader reports nothing, so its tiles keep the "—" they were
    /// built with.</summary>
    [Fact]
    public void Read_VendorWithoutAReader_ReportsNothing() {
        using var provider = new WindowsGpuSensorProvider(new[] { new FakeReader(0x10DE, new GpuSensorSample(41, 16.42)) });

        Assert.Equal(GpuSensorSample.None, provider.Read("gpu-b", Amd));
    }

    /// <summary>DXGI can fail to report a PCI identity; that must not be treated as a vendor.</summary>
    [Fact]
    public void Read_NoPciIdentity_ReportsNothing() {
        using var provider = new WindowsGpuSensorProvider(new[] { new FakeReader(0x10DE, new GpuSensorSample(41, 16.42)) });

        Assert.Equal(GpuSensorSample.None, provider.Read("gpu-a", null));
    }

    /// <summary>Readers are contracted never to throw. One that does is dropped for the rest of the session
    /// rather than re-entered every tick.</summary>
    [Fact]
    public void Read_ReaderThrows_IsContainedAndNotRetried() {
        var reader = new ThrowingReader(0x10DE);
        using var provider = new WindowsGpuSensorProvider(new[] { reader });

        Assert.Equal(GpuSensorSample.None, provider.Read("gpu-a", Nvidia));
        Assert.Equal(1, reader.Calls);
        Assert.True(reader.Disposed);

        Assert.Equal(GpuSensorSample.None, provider.Read("gpu-a", Nvidia));
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public void Dispose_DisposesEveryReader() {
        var first = new FakeReader(0x10DE);
        var second = new FakeReader(0x1002);
        var provider = new WindowsGpuSensorProvider(new IGpuSensorReader[] { first, second });

        provider.Dispose();

        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    private sealed class FakeReader(uint vendorId, GpuSensorSample sample = default) : IGpuSensorReader {
        public uint VendorId => vendorId;
        public bool Disposed { get; private set; }
        public GpuSensorSample Read(string adapterKey, GpuPciId pci) => sample;
        public void Dispose() => Disposed = true;
    }

    private sealed class ThrowingReader(uint vendorId) : IGpuSensorReader {
        public uint VendorId => vendorId;
        public int Calls { get; private set; }
        public bool Disposed { get; private set; }
        public GpuSensorSample Read(string adapterKey, GpuPciId pci) {
            Calls++;
            throw new InvalidOperationException("reader is broken");
        }
        public void Dispose() => Disposed = true;
    }
}
