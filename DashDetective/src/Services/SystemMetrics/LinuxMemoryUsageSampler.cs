using DashDetective.Services.Platform.Linux;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// System physical-memory usage on Linux, from <c>/proc/meminfo</c>. Used memory is
/// <c>MemTotal − MemAvailable</c>, the closest analogue to what Windows reports as the memory load:
/// <c>MemAvailable</c> is the kernel's own estimate of what a new workload could claim without swapping,
/// so it already discounts reclaimable cache the way Task Manager's figure does. This is also what
/// <c>free -h</c> prints as "available", which is what makes the two agree.
///
/// Stateless and never throws — memory is an absolute reading, so there is nothing to diff. An
/// unreadable <c>/proc/meminfo</c> yields the same zeroed sample as an inert Windows sampler.
/// </summary>
internal sealed class LinuxMemoryUsageSampler : IMemoryUsageSampler {
    // Concatenated forward-slash literal, never Path.Combine — see IProcFileSystem.
    private const string MeminfoPath = "/proc/meminfo";

    private readonly IProcFileSystem _proc;

    public LinuxMemoryUsageSampler() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the parse and its fallbacks can be exercised against
    /// canned fixtures from any dev machine.</summary>
    internal LinuxMemoryUsageSampler(IProcFileSystem proc) => _proc = proc;

    /// <summary>Returns the current snapshot, or a zeroed sample when <c>/proc/meminfo</c> reports no
    /// total.</summary>
    public MemorySample Sample() {
        var fields = ProcMeminfoParser.Parse(_proc.ReadAllLines(MeminfoPath));

        var total = ProcMeminfoParser.Value(fields, "MemTotal");
        if (total == 0)
            return new MemorySample(0, 0, 0, 0, 0);

        var available = Available(fields);
        var used = total >= available ? total - available : 0;

        // Committed_AS can legitimately exceed CommitLimit under overcommit, so neither is clamped to the
        // other; the Performance tile shows the pair as the kernel reports it.
        return new MemorySample(
            used * 100.0 / total,
            used,
            total,
            ProcMeminfoParser.Value(fields, "Committed_AS"),
            ProcMeminfoParser.Value(fields, "CommitLimit"));
    }

    /// <summary>Memory a new workload could claim. Prefers the kernel's <c>MemAvailable</c> estimate and
    /// falls back to <c>MemFree + Cached + Buffers</c>, which is what pre-3.14 kernels force — a coarser
    /// figure, but far better than reporting the machine as fully used.</summary>
    private static ulong Available(IReadOnlyDictionary<string, ulong> fields) {
        var available = ProcMeminfoParser.Value(fields, "MemAvailable");
        if (available > 0)
            return available;

        return ProcMeminfoParser.Value(fields, "MemFree")
             + ProcMeminfoParser.Value(fields, "Cached")
             + ProcMeminfoParser.Value(fields, "Buffers");
    }
}
