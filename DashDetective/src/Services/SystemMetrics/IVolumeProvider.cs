using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// One mounted volume: its host physical-disk number (for the drive-card rollup, <c>null</c> when it can't
/// be resolved), drive letter (<c>null</c> for unlettered partitions like Recovery/EFI), label, file
/// system, and total/free bytes. Sizes are raw so callers format them with <c>FileSizeFormatter</c>.
/// <c>GptType</c> is the host partition's raw GPT type GUID (empty on MBR disks or when unresolved) —
/// callers turn it into a display name with <c>PartitionTypeFormatter</c>.
/// </summary>
public readonly record struct VolumeInfo(
    int? DiskNumber, char? DriveLetter, string Label, string FileSystem, ulong SizeBytes, ulong FreeBytes,
    string GptType = "");

/// <summary>Enumerates the machine's volumes — including the unlettered Recovery/EFI partitions
/// <c>System.IO.DriveInfo</c> omits. Implementations must never throw: any failure yields an empty
/// list.</summary>
internal interface IVolumeProvider {
    Task<IReadOnlyList<VolumeInfo>> GetAsync();
}
