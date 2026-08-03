using DashDetective.Services.SystemMetrics;
using DashDetective.Tabs.Dashboard;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>Builds a <see cref="HardwareProviders"/> set from canned values, so a page can be driven
/// without WMI. Shared because the Dashboard and Storage tests both need the same seven-member record
/// with one or two members overridden — the <c>MetricSamplers</c> faking pattern.</summary>
internal static class StubHardwareProviders {
    public static HardwareProviders With(
        CpuStaticInfo? cpu = null,
        MemoryStaticInfo? memory = null,
        SystemStaticInfo? system = null,
        IReadOnlyList<GpuAdapter>? gpuAdapters = null,
        IReadOnlyList<PhysicalDiskInfo>? disks = null,
        IReadOnlyList<VolumeInfo>? volumes = null,
        Func<int, double?>? diskTemperature = null) =>
        new(new StubCpu(cpu ?? CpuStaticInfo.Unknown),
            new StubMemory(memory ?? MemoryStaticInfo.Unknown),
            new StubSystem(system ?? SystemStaticInfo.Unknown),
            new StubGpuAdapters(gpuAdapters ?? []),
            new StubDisks(disks ?? []),
            new StubVolumes(volumes ?? []),
            new StubTemperature(diskTemperature ?? (_ => null)));

    private sealed class StubCpu(CpuStaticInfo info) : ICpuInfoProvider {
        public Task<CpuStaticInfo> GetAsync() => Task.FromResult(info);
    }

    private sealed class StubMemory(MemoryStaticInfo info) : IMemoryInfoProvider {
        public Task<MemoryStaticInfo> GetAsync() => Task.FromResult(info);
    }

    private sealed class StubSystem(SystemStaticInfo info) : ISystemInfoProvider {
        public Task<SystemStaticInfo> GetAsync() => Task.FromResult(info);
    }

    private sealed class StubGpuAdapters(IReadOnlyList<GpuAdapter> adapters) : IGpuAdapterProvider {
        public Task<IReadOnlyList<GpuAdapter>> GetAsync() => Task.FromResult(adapters);
    }

    private sealed class StubDisks(IReadOnlyList<PhysicalDiskInfo> disks) : IPhysicalDiskProvider {
        public Task<IReadOnlyList<PhysicalDiskInfo>> GetAsync() => Task.FromResult(disks);
    }

    private sealed class StubVolumes(IReadOnlyList<VolumeInfo> volumes) : IVolumeProvider {
        public Task<IReadOnlyList<VolumeInfo>> GetAsync() => Task.FromResult(volumes);
    }

    private sealed class StubTemperature(Func<int, double?> read) : IDiskTemperatureProvider {
        public double? ReadCelsius(int deviceId) => read(deviceId);
    }
}
