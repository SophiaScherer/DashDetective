using DashDetective.Services.Network;
using DashDetective.Shared;
using DashDetective.Tabs.Dashboard;
using DashDetective.Tabs.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>The kind of hardware a <see cref="DeviceInstance"/> represents. Both the Dashboard and the
/// Performance tab iterate categories to group like devices together.</summary>
public enum DeviceCategory { Cpu, Memory, Gpu, Disk, Network }

/// <summary>
/// One detected hardware device's static identity — its <see cref="Category"/>, a stable <see cref="Id"/>
/// (for selection/persistence), and the same display trio the Performance rail already renders
/// (<see cref="Name"/> / <see cref="Sub"/> / <see cref="Spec"/>). Live values (utilisation %, throughput)
/// are not carried here: the view models overlay those per tick, keyed by <see cref="Id"/> /
/// <see cref="DiskNumber"/>.
/// </summary>
public sealed record DeviceInstance(
    DeviceCategory Category, string Id, string Name, string Sub, string Spec,
    int? DiskNumber = null, string? GpuLuid = null);

/// <summary>
/// The single source of truth for "what hardware exists, grouped by kind." Composes the existing static-info
/// providers (<see cref="CpuInfoProvider"/>, <see cref="MemoryInfoProvider"/>, <see cref="GpuAdapterProvider"/>,
/// <see cref="PhysicalDiskProvider"/> + <see cref="VolumeProvider"/>, and the primary network adapter) into an
/// ordered list of <see cref="DeviceInstance"/>s. Disks and GPUs are multi-instance (N drives, N physical
/// adapters); CPU/Memory/Network are single, but the shape is instance-count-agnostic so multi-socket
/// enumeration would light up the same UI once a sampler enumerates it.
///
/// <see cref="Compose"/> is pure (no IO/UI) and unit-tested directly, mirroring <see cref="StorageComposer"/>;
/// <see cref="LoadAsync"/> is the thin wrapper that fetches from the providers off the UI thread.
/// </summary>
public sealed class DeviceInventory {
    private readonly IReadOnlyList<DeviceInstance> _instances;

    public DeviceInventory(IEnumerable<DeviceInstance> instances) =>
        _instances = instances.ToList();

    /// <summary>Every detected instance, in grouped order (CPUs → Memory → GPUs → Disks → Network).</summary>
    public IReadOnlyList<DeviceInstance> Instances => _instances;

    /// <summary>Every instance of one category, in detection order (e.g. all disks by disk number).</summary>
    public IReadOnlyList<DeviceInstance> All(DeviceCategory category) =>
        _instances.Where(d => d.Category == category).ToList();

    /// <summary>The first instance of a category (the "primary" of its kind), or <c>null</c> if none was
    /// detected. What the Performance rail's "Primary" filter and the singleton categories use.</summary>
    public DeviceInstance? Primary(DeviceCategory category) =>
        _instances.FirstOrDefault(d => d.Category == category);

    /// <summary>
    /// Reads every provider once (off the UI thread) and composes the inventory. Each provider soft-fails to
    /// its <c>Unknown</c>/empty value, so a partial read still yields a usable inventory rather than throwing.
    /// </summary>
    public static async Task<DeviceInventory> LoadAsync() {
        var cpuTask = CpuInfoProvider.GetAsync();
        var memoryTask = MemoryInfoProvider.GetAsync();
        var gpusTask = GpuAdapterProvider.GetAsync();
        var gpuLuidsTask = Task.Run(SampleActiveGpuLuids);
        var disksTask = PhysicalDiskProvider.GetAsync();
        var volumesTask = VolumeProvider.GetAsync();
        var networkTask = Task.Run(ReadNetwork);
        await Task.WhenAll(cpuTask, memoryTask, gpusTask, gpuLuidsTask, disksTask, volumesTask, networkTask);

        var (networkName, networkSpec) = networkTask.Result;
        return Compose(cpuTask.Result, memoryTask.Result, gpusTask.Result, gpuLuidsTask.Result,
                       disksTask.Result, volumesTask.Result, networkName, networkSpec);
    }

    /// <summary>The LUID tokens present in the PDH GPU-engine counters right now — the adapters actually
    /// backed by a driver. <see cref="Compose"/> intersects the DXGI adapter list with this set so phantom
    /// duplicate LUIDs (DXGI can list one GPU under several) are dropped. Soft-fails to an empty set.</summary>
    private static IReadOnlySet<string> SampleActiveGpuLuids() {
        try {
            using var sampler = new GpuUsageSampler();
            return sampler.SampleAdapters().Keys.ToHashSet(StringComparer.Ordinal);
        } catch {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Builds the inventory from already-fetched provider results. Pure and side-effect-free (no WMI, IO or
    /// UI) so it is unit-tested directly. Disk names reuse <see cref="StorageComposer"/> (lowest-lettered
    /// volume, "Local Disk" when unlabelled, model when unlettered); CPU/Memory/GPU formatting matches the
    /// Performance rail's captions.
    /// </summary>
    public static DeviceInventory Compose(
        CpuStaticInfo cpu, MemoryStaticInfo memory, IReadOnlyList<GpuAdapter> gpus, IReadOnlySet<string> activeGpuLuids,
        IReadOnlyList<PhysicalDiskInfo> disks, IReadOnlyList<VolumeInfo> volumes,
        string networkName, string networkSpec) {
        var instances = new List<DeviceInstance> {
            new(DeviceCategory.Cpu, "cpu", "CPU", FormatCpuSub(cpu), HardwareNameFormatter.ShortenCpu(cpu.Name)),
            // Memory's caption is the live used/total figure the view model fills each tick, so the static Sub
            // is blank; the Spec carries the module type/speed/slots.
            new(DeviceCategory.Memory, "mem", "Memory", "", FormatMemorySpec(memory)),
        };

        // GPUs: one instance per real adapter — DXGI's non-software adapters intersected with the LUIDs
        // actually present in the PDH engine counters (drops software adapters and the phantom duplicate
        // LUIDs DXGI lists a single GPU under). A lone GPU keeps the plain "GPU" label; several are indexed
        // "GPU 0", "GPU 1", … (the design comp), each keyed by its LUID for the samplers to join on.
        var realGpus = gpus.Where(g => !g.IsSoftware && activeGpuLuids.Contains(g.LuidToken)).ToList();
        for (var i = 0; i < realGpus.Count; i++) {
            var gpu = realGpus[i];
            var name = realGpus.Count > 1 ? $"GPU {i.ToString(CultureInfo.InvariantCulture)}" : "GPU";
            instances.Add(new DeviceInstance(
                DeviceCategory.Gpu, $"gpu:{gpu.LuidToken}", name,
                HardwareNameFormatter.ShortenGpu(gpu.Name), gpu.Name, GpuLuid: gpu.LuidToken));
        }

        // Disks are the one multi-instance category today. Reuse StorageComposer for the display name (keyed by
        // disk number), then join with the physical-disk record for its type label + model/size spec.
        var namesByDisk = StorageComposer.Compose(disks, volumes).ToDictionary(d => d.DiskNumber, d => d.Name);
        foreach (var disk in disks.OrderBy(d => d.DeviceId)) {
            var name = namesByDisk.TryGetValue(disk.DeviceId, out var composed) ? composed : $"Disk {disk.DeviceId}";
            instances.Add(new DeviceInstance(
                DeviceCategory.Disk, $"disk:{disk.DeviceId.ToString(CultureInfo.InvariantCulture)}",
                name, string.IsNullOrEmpty(disk.TypeLabel) ? "Drive" : disk.TypeLabel,
                FormatDiskSpec(disk.Model, disk.SizeBytes), disk.DeviceId));
        }

        // Network's caption (link speed) is filled live, so the static Sub is blank; the Spec is the adapter
        // description.
        instances.Add(new DeviceInstance(DeviceCategory.Network, "net", networkName, "", networkSpec));

        return new DeviceInventory(instances);
    }

    /// <summary>Cores plus base clock, e.g. "24 cores · 3.2 GHz" (matches the Performance rail).</summary>
    private static string FormatCpuSub(CpuStaticInfo info) {
        var cores = info.PhysicalCores > 0 ? info.PhysicalCores : info.LogicalCores;
        if (cores > 0 && info.MaxClockMhz > 0)
            return $"{cores} cores · {(info.MaxClockMhz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)} GHz";
        return cores > 0 ? $"{cores} cores" : "";
    }

    /// <summary>Type, speed and slot count, e.g. "DDR5-6000 · 2 slots".</summary>
    private static string FormatMemorySpec(MemoryStaticInfo info) {
        var label = info.SpeedMhz > 0
            ? $"{info.TypeLabel}-{info.SpeedMhz.ToString(CultureInfo.InvariantCulture)}"
            : info.TypeLabel;
        return info.ModuleCount > 0
            ? $"{label} · {info.ModuleCount.ToString(CultureInfo.InvariantCulture)} slots"
            : label;
    }

    /// <summary>Model plus capacity, e.g. "Samsung SSD 990 Pro 1.8 TB" (binary TB/GB, like the drive cards).</summary>
    private static string FormatDiskSpec(string model, ulong sizeBytes) {
        if (sizeBytes == 0)
            return model;
        var gb = sizeBytes / (1024.0 * 1024 * 1024);
        var size = gb >= 1024
            ? $"{(gb / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} TB"
            : $"{gb.ToString("0", CultureInfo.InvariantCulture)} GB";
        return string.IsNullOrEmpty(model) ? size : $"{model} {size}";
    }

    /// <summary>Resolves the primary network adapter's name + description for the network instance, or neutral
    /// fallbacks when no adapter is available. Runs off the UI thread (adapter enumeration can be slow).</summary>
    private static (string Name, string Spec) ReadNetwork() {
        var adapter = NetworkUsageSampler.SelectPrimary();
        if (adapter is null)
            return ("Ethernet", "");
        var name = string.IsNullOrWhiteSpace(adapter.Name) ? "Ethernet" : adapter.Name;
        return (name, adapter.Description ?? "");
    }
}
