using System.Runtime.InteropServices;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// A single physical-memory snapshot: load as a percentage (0–100), plus used and total bytes.
/// </summary>
public readonly record struct MemorySample(double LoadPercent, ulong UsedBytes, ulong TotalBytes);

/// <summary>
/// Samples system physical-memory usage via the Win32 <c>GlobalMemoryStatusEx</c> API. Each
/// <see cref="Sample"/> call returns an absolute snapshot (memory load percentage plus used/total
/// bytes) at the moment of the call. No dependencies, negligible per-sample cost.
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

    /// <summary>
    /// Returns the current physical-memory snapshot. <c>MemoryLoad</c> is used directly for the
    /// percentage; used bytes are total − available. Memory is an absolute reading, so unlike the
    /// CPU sampler there is no prior state to seed or diff.
    /// </summary>
    public MemorySample Sample() {
        // Length must be set before the call so the OS knows the struct version/size.
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
            return new MemorySample(0, 0, 0);

        var used = status.TotalPhys >= status.AvailPhys
            ? status.TotalPhys - status.AvailPhys
            : 0;

        // Clamp defensively against rounding edge cases.
        var load = status.MemoryLoad > 100 ? 100 : status.MemoryLoad;

        return new MemorySample(load, used, status.TotalPhys);
    }
}
