using System;
using System.Collections.Generic;

namespace DashDetective.Tabs.Storage;

/// <summary>Turns a partition's raw GPT type GUID into the Partitions table's Type column — the reason an
/// unlabelled volume like the 833 MB Recovery partition can still be identified. Pure, so it is unit-tested
/// directly.</summary>
public static class PartitionTypeFormatter {
    // The GPT partition types Windows puts on a normal system disk. Add a line to cover more.
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase) {
        ["de94bba4-06d1-4d40-a16a-bfd50179d6ac"] = "Recovery",
        ["c12a7328-f81f-11d2-ba4b-00a0c93ec93b"] = "EFI System",
        ["e3c9e316-0b5c-4db8-817d-f92df00215ae"] = "Reserved",
        ["ebd0a0a2-b9e5-4433-87c0-68b6b72699c7"] = "Data",
    };

    /// <summary>Names the partition type, e.g. "Recovery". An unrecognised or absent GUID (MBR disks report
    /// none) falls back to "Data" for a mounted lettered volume, otherwise "—".</summary>
    public static string Format(string? gptType, bool hasDriveLetter) {
        var key = (gptType ?? "").Trim().Trim('{', '}');
        if (Names.TryGetValue(key, out var name))
            return name;
        return hasDriveLetter ? "Data" : "—";
    }
}
