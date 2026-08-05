using DashDetective.Services.Platform.Linux;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Total CPU utilisation on Linux, from the aggregate <c>cpu</c> line of <c>/proc/stat</c> — the same
/// counters <c>top</c> and GNOME System Monitor read. The Linux arm of <see cref="ICpuSampler"/>,
/// selected by <see cref="CpuUsageSampler"/>'s constructor; the PDH samplers are the Windows arms.
///
/// <b>Stateful</b> — it holds the previous jiffy snapshot, so it must not be added to
/// <c>HardwareProviders</c>, whose members are required to be stateless. Nothing here can throw: an
/// absent or malformed <c>/proc/stat</c> yields 0, matching the other samplers' soft-fail contract.
/// </summary>
internal sealed class LinuxCpuSampler : ICpuSampler {
    // Concatenated forward-slash literal, never Path.Combine — see IProcFileSystem.
    private const string StatPath = "/proc/stat";

    private readonly IProcFileSystem _proc;
    private ulong _prevBusy;
    private ulong _prevTotal;

    public LinuxCpuSampler() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so the parser and the diff can be exercised against
    /// canned fixtures from any dev machine.</summary>
    internal LinuxCpuSampler(IProcFileSystem proc) {
        _proc = proc;
        // Seed a snapshot so the first Sample() reflects a real interval rather than the time since boot.
        TryReadAggregate(out _prevBusy, out _prevTotal);
    }

    /// <summary>Returns CPU utilisation (0–100) for the interval since the previous call. A missing or
    /// unreadable <c>/proc/stat</c> yields 0.</summary>
    public double Sample() {
        if (!TryReadAggregate(out var busy, out var total))
            return 0;

        // The counters are monotonic, so a backwards or non-advancing read means there is nothing to
        // report this tick; the snapshot still moves forward so the next interval is measured correctly.
        var busyDelta = busy > _prevBusy ? busy - _prevBusy : 0;
        var totalDelta = total > _prevTotal ? total - _prevTotal : 0;
        _prevBusy = busy;
        _prevTotal = total;

        return ProcStatParser.ComputeUsage(busyDelta, totalDelta);
    }

    /// <summary>Reads the aggregate <c>cpu</c> line — the roll-up across every core, which precedes the
    /// per-core <c>cpu0</c>… lines.</summary>
    private bool TryReadAggregate(out ulong busy, out ulong total) {
        foreach (var line in _proc.ReadAllLines(StatPath)) {
            if (ProcStatParser.TryParseCpuLine(line, out var label, out busy, out total) && label == "cpu")
                return true;
        }

        busy = 0;
        total = 0;
        return false;
    }
}
