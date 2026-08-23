using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>Builds a <see cref="HardwareProviders"/> set from canned values, so a page can be driven
/// without WMI. Shared because the Dashboard and Storage tests both need the same seven-member record
/// with one or two members overridden — the <c>MetricSamplers</c> faking pattern.
///
/// <see cref="With"/> takes values and always succeeds; <see cref="Compose"/> takes a factory per
/// section, so a test can hand one a faulted task or a synchronous throw. That second form is what makes
/// the pages' own soft-fail paths reachable at all — with values only, every catch in the Dashboard and
/// Storage view models was dead code from the suite's point of view. Same shape as the sibling
/// <see cref="StubHardwareInfoProviders"/>, which the Hardware tab's tests already use this way.</summary>
internal static class StubHardwareProviders {
    public static HardwareProviders With(
        CpuStaticInfo? cpu = null,
        MemoryStaticInfo? memory = null,
        SystemStaticInfo? system = null,
        IReadOnlyList<GpuAdapter>? gpuAdapters = null,
        IReadOnlyList<PhysicalDiskInfo>? disks = null,
        IReadOnlyList<VolumeInfo>? volumes = null,
        Func<int, double?>? diskTemperature = null) =>
        Compose(
            cpu: () => Task.FromResult(cpu ?? CpuStaticInfo.Unknown),
            memory: () => Task.FromResult(memory ?? MemoryStaticInfo.Unknown),
            system: () => Task.FromResult(system ?? SystemStaticInfo.Unknown),
            gpuAdapters: () => Task.FromResult(gpuAdapters ?? []),
            disks: () => Task.FromResult(disks ?? []),
            volumes: () => Task.FromResult(volumes ?? []),
            diskTemperature: diskTemperature);

    /// <summary>The failable form: each section is invoked inside its provider, so a factory may return a
    /// value, return a faulted task, or throw before returning one — the three shapes a real reader can
    /// fail in.</summary>
    public static HardwareProviders Compose(
        Func<Task<CpuStaticInfo>>? cpu = null,
        Func<Task<MemoryStaticInfo>>? memory = null,
        Func<Task<SystemStaticInfo>>? system = null,
        Func<Task<IReadOnlyList<GpuAdapter>>>? gpuAdapters = null,
        Func<Task<IReadOnlyList<PhysicalDiskInfo>>>? disks = null,
        Func<Task<IReadOnlyList<VolumeInfo>>>? volumes = null,
        Func<int, double?>? diskTemperature = null) =>
        new(new StubCpu(cpu ?? (() => Task.FromResult(CpuStaticInfo.Unknown))),
            new StubMemory(memory ?? (() => Task.FromResult(MemoryStaticInfo.Unknown))),
            new StubSystem(system ?? (() => Task.FromResult(SystemStaticInfo.Unknown))),
            new StubGpuAdapters(gpuAdapters ?? (() => Task.FromResult<IReadOnlyList<GpuAdapter>>([]))),
            new StubDisks(disks ?? (() => Task.FromResult<IReadOnlyList<PhysicalDiskInfo>>([]))),
            new StubVolumes(volumes ?? (() => Task.FromResult<IReadOnlyList<VolumeInfo>>([]))),
            new StubTemperature(diskTemperature ?? (_ => null)));

    /// <summary>A reader that fails the way a dead WMI namespace does — for the <c>Compose</c> arguments
    /// above, so a test reads as "this section is broken" rather than as lambda plumbing.</summary>
    public static Func<Task<T>> Fails<T>(string why = "the provider is unavailable") =>
        () => Task.FromException<T>(new InvalidOperationException(why));

    private sealed class StubCpu(Func<Task<CpuStaticInfo>> read) : ICpuInfoProvider {
        public Task<CpuStaticInfo> GetAsync() => read();
    }

    private sealed class StubMemory(Func<Task<MemoryStaticInfo>> read) : IMemoryInfoProvider {
        public Task<MemoryStaticInfo> GetAsync() => read();
    }

    private sealed class StubSystem(Func<Task<SystemStaticInfo>> read) : ISystemInfoProvider {
        public Task<SystemStaticInfo> GetAsync() => read();
    }

    private sealed class StubGpuAdapters(Func<Task<IReadOnlyList<GpuAdapter>>> read) : IGpuAdapterProvider {
        public Task<IReadOnlyList<GpuAdapter>> GetAsync() => read();
    }

    private sealed class StubDisks(Func<Task<IReadOnlyList<PhysicalDiskInfo>>> read) : IPhysicalDiskProvider {
        public Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync() => read();
    }

    private sealed class StubVolumes(Func<Task<IReadOnlyList<VolumeInfo>>> read) : IVolumeProvider {
        public Task<IReadOnlyList<VolumeInfo>> GetAsync() => read();
    }

    private sealed class StubTemperature(Func<int, double?> read) : IDiskTemperatureProvider {
        public double? ReadCelsius(int deviceId) => read(deviceId);
    }
}
