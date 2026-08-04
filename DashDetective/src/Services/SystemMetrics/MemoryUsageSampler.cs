using System;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A single physical-memory snapshot: load as a percentage (0–100), used and total physical bytes,
/// plus the system commit charge and limit (<c>CommittedBytes</c> of <c>CommitLimitBytes</c>) — Task
/// Manager's "Committed" figure, which counts pagefile-backed virtual memory beyond physical RAM.
/// </summary>
public readonly record struct MemorySample(
    double LoadPercent, ulong UsedBytes, ulong TotalBytes,
    ulong CommittedBytes, ulong CommitLimitBytes);

/// <summary>
/// Samples system physical-memory usage via the Win32 <c>GlobalMemoryStatusEx</c> API. Each
/// <see cref="Sample"/> call returns an absolute snapshot (memory load percentage plus used/total
/// bytes) at the moment of the call. No dependencies, negligible per-sample cost.
///
/// Shared: the Dashboard and the Processes tab each own an instance (the Processes summary strip
/// shows the same system-wide Memory% as the Dashboard). Moved here from src/Tabs/Dashboard with
/// sign-off when the Processes tab was activated — the same precedent as <c>NetworkUsageSampler</c>.
///
/// Unlike the PDH samplers there is no query to stand up in a constructor, so the soft-fail contract
/// lives in <see cref="Sample"/>: a <c>kernel32</c> load failure latches the sampler inert, and every
/// later call returns a zeroed reading without re-entering the native call.
/// </summary>
public sealed class MemoryUsageSampler {
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>The "nothing to report" reading — an API failure, or a sampler latched inert.</summary>
    private static readonly MemorySample NoReading = new(0, 0, 0, 0, 0);

    // Latches false the first time kernel32 can't be bound, so that failure is caught and logged once
    // rather than thrown and swallowed on every tick of the shared 1 Hz timer.
    private bool _available = true;

    public MemoryUsageSampler() { }

    /// <summary>Test seam: starts the sampler latched inert so the soft-fail contract can be exercised on
    /// a healthy host, where the native call always succeeds.</summary>
    internal MemoryUsageSampler(SamplerInit _) => _available = false;

    /// <summary>
    /// Returns the current physical-memory snapshot. <c>MemoryLoad</c> is used directly for the
    /// percentage; used bytes are total − available. Memory is an absolute reading, so unlike the
    /// CPU sampler there is no prior state to seed or diff. Yields a zeroed reading once the sampler
    /// has latched inert.
    /// </summary>
    public MemorySample Sample() {
        if (!_available)
            return NoReading;

        // Length must be set before the call so the OS knows the struct version/size.
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        try {
            if (!GlobalMemoryStatusEx(ref status))
                return NoReading;
        } catch (Exception ex) when (NativeLoadFailure.Matches(ex)) {
            _available = false;
            NativeLoadFailure.Report(nameof(MemoryUsageSampler), ex);
            return NoReading;
        }

        var used = status.TotalPhys >= status.AvailPhys
            ? status.TotalPhys - status.AvailPhys
            : 0;

        // Committed = commit limit − amount the system can still commit. TotalPageFile is the current
        // commit limit (RAM + pagefile); AvailPageFile is what remains commitable.
        var committed = status.TotalPageFile >= status.AvailPageFile
            ? status.TotalPageFile - status.AvailPageFile
            : 0;

        // Clamp defensively against rounding edge cases.
        var load = status.MemoryLoad > 100 ? 100 : status.MemoryLoad;

        return new MemorySample(load, used, status.TotalPhys, committed, status.TotalPageFile);
    }
}
