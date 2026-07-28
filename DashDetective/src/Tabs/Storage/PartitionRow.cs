namespace DashDetective.Tabs.Storage;

/// <summary>
/// One row in the Storage tab's Partitions table: volume letter, label, partition type, file system,
/// capacity and free space. Static structural info — plain display strings, rebuilt as a set rather than
/// mutated in place, so no change notification is needed.
/// </summary>
public sealed class PartitionRow {
    /// <summary>Volume letter (e.g. "C:"), or "—" for a partition with no drive letter.</summary>
    public string Vol { get; init; } = "";

    /// <summary>Volume label (e.g. "Windows"), or "Local Disk" when a lettered volume has none.</summary>
    public string Label { get; init; } = "";

    /// <summary>File system (e.g. "NTFS", "FAT32").</summary>
    public string FileSystem { get; init; } = "";

    /// <summary>Partition type from its GPT type GUID (e.g. "Recovery", "Data").</summary>
    public string Type { get; init; } = "—";

    /// <summary>Formatted total capacity (e.g. "2.0 TB").</summary>
    public string Capacity { get; init; } = "";

    /// <summary>Formatted free space (e.g. "640 GB").</summary>
    public string Free { get; init; } = "";
}
