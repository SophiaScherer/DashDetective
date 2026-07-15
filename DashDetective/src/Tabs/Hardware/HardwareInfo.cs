using System.Collections.Generic;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// A one-shot snapshot of the machine's static hardware facts, read via WMI by
/// <see cref="HardwareInfoProvider"/> and mapped onto the Hardware cards by
/// <c>HardwareViewModel</c>. Each per-card sub-record carries <b>display-ready</b> strings (the WMI
/// formatting — mV→V, KB→MB, etc. — lives in the provider), each defaulting to the neutral
/// placeholder "—" so a field WMI cannot supply simply stays "—". The <c>Sensors</c> card has no
/// sub-record here: live thermals/fans/voltages are deferred and it keeps its placeholders.
/// </summary>
public sealed record HardwareInfo(
    ProcessorInfo Processor,
    MemoryInfo Memory,
    StorageInfo Storage,
    MotherboardInfo Motherboard,
    GraphicsInfo Graphics) {
    public static HardwareInfo Unknown { get; } = new(
        ProcessorInfo.Unknown, MemoryInfo.Unknown, StorageInfo.Unknown,
        MotherboardInfo.Unknown, GraphicsInfo.Unknown);
}

/// <summary>Processor card — <c>Name</c> is the card subtitle; the rest are the spec-row values.</summary>
public sealed record ProcessorInfo(
    string Name = "—",
    string CoresThreads = "—",
    string BaseBoost = "—",
    string CacheL3 = "—",
    string Tdp = "—",
    string Socket = "—") {
    public static ProcessorInfo Unknown { get; } = new();
}

/// <summary>Memory card — <c>Summary</c> is the card subtitle (e.g. "32 GB DDR5-6000").</summary>
public sealed record MemoryInfo(
    string Summary = "—",
    string Installed = "—",
    string Speed = "—",
    string Timings = "—",
    string SlotsUsed = "—",
    string Voltage = "—") {
    public static MemoryInfo Unknown { get; } = new();
}

/// <summary>One physical drive: <c>Model</c> is the row key, <c>Detail</c> the value (e.g. "2 TB NVMe").</summary>
public sealed record StorageDeviceInfo(string Model, string Detail);

/// <summary>Storage Devices card — variable rows: one per <see cref="Drives"/> entry plus a health row.</summary>
public sealed record StorageInfo(
    string Summary,
    IReadOnlyList<StorageDeviceInfo> Drives,
    string TotalHealth = "—") {
    public static StorageInfo Unknown { get; } = new("—", new List<StorageDeviceInfo>());
}

/// <summary>Motherboard card — <c>Board</c> is the card subtitle (vendor + product).</summary>
public sealed record MotherboardInfo(
    string Board = "—",
    string Chipset = "—",
    string Bios = "—",
    string FormFactor = "—",
    string PcieSlots = "—",
    string M2Slots = "—") {
    public static MotherboardInfo Unknown { get; } = new();
}

/// <summary>Graphics card — <c>Name</c> is the card subtitle (adapter name).</summary>
public sealed record GraphicsInfo(
    string Name = "—",
    string Memory = "—",
    string CudaCores = "—",
    string BoostClock = "—",
    string Driver = "—",
    string Bus = "—") {
    public static GraphicsInfo Unknown { get; } = new();
}
