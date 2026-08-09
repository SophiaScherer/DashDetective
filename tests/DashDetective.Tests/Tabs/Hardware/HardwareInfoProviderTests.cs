using DashDetective.Tabs.Hardware;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Hardware;

/// <summary>Covers the <see cref="IHardwareInfoProvider"/> seam: which implementation the platform
/// resolves to, that the unsupported one degrades to exactly the placeholder snapshot the old inline
/// <c>OperatingSystem.IsWindows()</c> guard returned, and that the Windows composite keeps each card
/// independent — a reader that breaks its never-throw contract costs only its own card.
///
/// <see cref="HardwareViewModel"/> itself is not covered here — its constructor touches
/// <c>HardwareIcons</c>, whose static initialiser calls <c>Geometry.Parse</c> and needs a render
/// backend these tests deliberately don't have (see the Testing conventions in AGENTS.md).</summary>
public class HardwareInfoProviderTests {
    /// <summary>Windows and Linux both compose the portable <see cref="HardwareInfoProvider"/> — they
    /// differ in which readers go into it, not in the composition. Only a platform with no readers at all
    /// falls through to the unsupported set.</summary>
    [Fact]
    public void ForCurrentPlatform_ResolvesTheReaderForThisHost() {
        var provider = IHardwareInfoProvider.ForCurrentPlatform();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            Assert.IsType<HardwareInfoProvider>(provider);
        else
            Assert.IsType<UnsupportedHardwareInfoProvider>(provider);
    }

    /// <summary>
    /// Both platforms compose the same portable provider, so its type no longer says which readers went
    /// into it — this does. The Linux set is filled in one milestone at a time, and the cards still on
    /// <c>Unsupported*</c> are the ones whose milestone has not landed; each moves here when it does.
    /// </summary>
    [Fact]
    public void ForCurrentPlatform_ComposesTheReadersForThisHost() {
        if (IHardwareInfoProvider.ForCurrentPlatform() is not HardwareInfoProvider provider)
            return;

        if (OperatingSystem.IsWindows()) {
            Assert.IsType<WindowsProcessorInfoProvider>(ReaderOf<IProcessorInfoProvider>(provider));
            Assert.IsType<WindowsMotherboardInfoProvider>(ReaderOf<IMotherboardInfoProvider>(provider));
            Assert.IsType<WindowsMemoryModulesProvider>(ReaderOf<IMemoryModulesProvider>(provider));
            Assert.IsType<WindowsStorageInfoProvider>(ReaderOf<IStorageInfoProvider>(provider));
            Assert.IsType<WindowsGraphicsInfoProvider>(ReaderOf<IGraphicsInfoProvider>(provider));
        } else {
            Assert.IsType<LinuxProcessorInfoProvider>(ReaderOf<IProcessorInfoProvider>(provider));
            Assert.IsType<LinuxMotherboardInfoProvider>(ReaderOf<IMotherboardInfoProvider>(provider));
            Assert.IsType<LinuxStorageInfoProvider>(ReaderOf<IStorageInfoProvider>(provider));
            Assert.IsType<UnsupportedMemoryModulesProvider>(ReaderOf<IMemoryModulesProvider>(provider));
            Assert.IsType<UnsupportedGraphicsInfoProvider>(ReaderOf<IGraphicsInfoProvider>(provider));
        }
    }

    /// <summary>Every per-card reader reports its <c>.Unknown</c> rather than throwing, whatever the
    /// platform hands back — the contract the composite's guard exists to backstop, checked here against
    /// the real readers on both CI legs.</summary>
    [Fact]
    public async Task Unsupported_PerCardReaders_ReportTheirUnknownRecord() {
        Assert.Same(MemoryInfo.Unknown, await new UnsupportedMemoryModulesProvider().GetAsync());
        Assert.Same(StorageInfo.Unknown, await new UnsupportedStorageInfoProvider().GetAsync());
        Assert.Same(GraphicsInfo.Unknown, await new UnsupportedGraphicsInfoProvider().GetAsync());
    }

    /// <summary>Reads back the reader a <see cref="HardwareInfoProvider"/> was built with — they are
    /// private fields, one per card interface. The same reflection shape
    /// <c>HardwareProvidersTests</c> uses for the shared temperature reader.</summary>
    private static object? ReaderOf<T>(HardwareInfoProvider provider) {
        foreach (var field in provider.GetType()
                     .GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            if (field.FieldType == typeof(T))
                return field.GetValue(provider);

        return null;
    }

    /// <summary>Every card reports <c>.Unknown</c>, so every field renders "—" rather than blanking.</summary>
    [Fact]
    public async Task Unsupported_GetAsync_ReportsUnknownForEveryCard() {
        var info = await new UnsupportedHardwareInfoProvider().GetAsync();

        Assert.Same(HardwareInfo.Unknown, info);
        Assert.Equal("—", info.Processor.Name);
        Assert.Equal("—", info.Memory.Summary);
        Assert.Equal("—", info.Storage.Summary);
        Assert.Equal("—", info.Motherboard.Chipset);
        Assert.Empty(info.Graphics.Adapters);
    }

    /// <summary>The real reader set never throws, whatever WMI or <c>/proc</c> does — the whole page
    /// depends on it, and each section is meant to fall back independently rather than propagate. Runs
    /// against whatever <c>ForCurrentPlatform</c> resolves, so it covers both CI legs.</summary>
    [Fact]
    public async Task ForCurrentPlatform_GetAsync_NeverThrows() {
        var info = await IHardwareInfoProvider.ForCurrentPlatform().GetAsync();

        Assert.NotNull(info.Processor);
        Assert.NotNull(info.Memory);
        Assert.NotNull(info.Storage);
        Assert.NotNull(info.Motherboard);
        Assert.NotNull(info.Graphics);
    }

    /// <summary>Each reader's result lands in its own slot — the composite wires five independent sources
    /// into one snapshot without crossing them over.</summary>
    [Fact]
    public async Task Compose_PlacesEachReadersResultInItsOwnSection() {
        var provider = StubHardwareInfoProviders.Compose(
            processor: () => Task.FromResult(new ProcessorInfo(Name: "AMD Ryzen 5 7600X", Socket: "AM5")),
            memory: () => Task.FromResult(new MemoryInfo(Summary: "32 GB DDR5-4800")),
            storage: () => Task.FromResult(new StorageInfo("1 drive · 2 TB total",
                new List<StorageDeviceInfo> { new("Sabrent SB-ROCKET", "2 TB NVMe") }, "Good")),
            motherboard: () => Task.FromResult(new MotherboardInfo(Board: "MSI MPG B650I", Chipset: "AMD B650")),
            graphics: () => Task.FromResult(new GraphicsInfo(
                new List<GraphicsAdapterInfo> { new(Name: "NVIDIA GeForce RTX 3060") })));

        var info = await provider.GetAsync();

        Assert.Equal("AMD Ryzen 5 7600X", info.Processor.Name);
        Assert.Equal("AM5", info.Processor.Socket);
        Assert.Equal("32 GB DDR5-4800", info.Memory.Summary);
        Assert.Equal("Good", info.Storage.TotalHealth);
        Assert.Equal("2 TB NVMe", Assert.Single(info.Storage.Drives).Detail);
        Assert.Equal("AMD B650", info.Motherboard.Chipset);
        Assert.Equal("NVIDIA GeForce RTX 3060", Assert.Single(info.Graphics.Adapters).Name);
    }

    /// <summary>The reason the cards were split apart: a reader that breaks its never-throw contract by
    /// returning a faulted task reports "—" for its own card only, instead of taking the whole snapshot
    /// down with it — which is what an unguarded <c>Task.WhenAll</c> would do.</summary>
    [Fact]
    public async Task Compose_WhenOneReaderFaults_OnlyThatCardDegrades() {
        var provider = StubHardwareInfoProviders.Compose(
            processor: () => Task.FromResult(new ProcessorInfo(Name: "AMD Ryzen 5 7600X")),
            memory: () => Task.FromResult(new MemoryInfo(Summary: "32 GB DDR5-4800")),
            motherboard: () => Task.FromResult(new MotherboardInfo(Board: "MSI MPG B650I")),
            graphics: () => Task.FromException<GraphicsInfo>(
                new InvalidOperationException("WMI is having a day")));

        var info = await provider.GetAsync();

        Assert.Same(GraphicsInfo.Unknown, info.Graphics);
        Assert.Equal("AMD Ryzen 5 7600X", info.Processor.Name);
        Assert.Equal("32 GB DDR5-4800", info.Memory.Summary);
        Assert.Equal("MSI MPG B650I", info.Motherboard.Board);
    }

    /// <summary>The other failure shape: a reader that throws <b>before</b> returning a task at all. The
    /// guard invokes each reader inside its try rather than only awaiting it, so this is caught too.</summary>
    [Fact]
    public async Task Compose_WhenOneReaderThrowsBeforeReturningATask_OnlyThatCardDegrades() {
        var provider = StubHardwareInfoProviders.Compose(
            processor: () => Task.FromResult(new ProcessorInfo(Name: "AMD Ryzen 5 7600X")),
            storage: () => throw new InvalidOperationException("the storage namespace is gone"));

        var info = await provider.GetAsync();

        Assert.Same(StorageInfo.Unknown, info.Storage);
        Assert.Equal("AMD Ryzen 5 7600X", info.Processor.Name);
    }

    /// <summary>All five readers failing is still a snapshot, not an exception — the page renders "—"
    /// everywhere rather than keeping its startup placeholders forever.</summary>
    [Fact]
    public async Task Compose_WhenEveryReaderFails_ReportsUnknownForEveryCard() {
        var provider = StubHardwareInfoProviders.Compose(
            processor: () => throw new InvalidOperationException(),
            memory: () => Task.FromException<MemoryInfo>(new InvalidOperationException()),
            storage: () => throw new InvalidOperationException(),
            motherboard: () => Task.FromException<MotherboardInfo>(new InvalidOperationException()),
            graphics: () => throw new InvalidOperationException());

        var info = await provider.GetAsync();

        Assert.Same(ProcessorInfo.Unknown, info.Processor);
        Assert.Same(MemoryInfo.Unknown, info.Memory);
        Assert.Same(StorageInfo.Unknown, info.Storage);
        Assert.Same(MotherboardInfo.Unknown, info.Motherboard);
        Assert.Same(GraphicsInfo.Unknown, info.Graphics);
    }
}
