using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A card's details from <c>/sys/class/drm</c> over the shared <see cref="DrmCardFacts"/>, keyed by its
/// <see cref="DrmCardFacts.Key"/> like every other GPU reader. The driver version is the module's own (none
/// for in-tree amdgpu), the location is the PCI address in Task Manager's words, and DirectX is always null —
/// Linux has none, so that row is omitted rather than filled. Stateless and never throws.
/// </summary>
internal sealed class LinuxGpuDetailsProvider : IGpuDetailsProvider {
    private readonly IProcFileSystem _proc;

    public LinuxGpuDetailsProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the walk runs against canned fixtures.</summary>
    internal LinuxGpuDetailsProvider(IProcFileSystem proc) => _proc = proc;

    public Task<IReadOnlyDictionary<string, GpuDetails>> GetAsync(IReadOnlyCollection<string> adapterKeys) =>
        Task.Run(() => Read(new HashSet<string>(adapterKeys, StringComparer.Ordinal)));

    private IReadOnlyDictionary<string, GpuDetails> Read(HashSet<string> wanted) {
        var details = new Dictionary<string, GpuDetails>(StringComparer.Ordinal);
        try {
            foreach (var card in DrmCardFacts.Read(_proc)) {
                if (!wanted.Contains(card.Key))
                    continue;
                details[card.Key] = new GpuDetails(
                    card.DriverVersion.Length > 0 ? card.DriverVersion : null,
                    null,
                    PciLocation.FromLinux(card.PciAddress)?.ToString());
            }
        } catch (Exception e) when (e is UnauthorizedAccessException or IOException) {
            // The real filesystem seam swallows these itself; this is for one that breaks its contract.
            Log.Warn("LinuxGpuDetailsProvider read failed", e);
            details.Clear();
        }
        return details;
    }
}
