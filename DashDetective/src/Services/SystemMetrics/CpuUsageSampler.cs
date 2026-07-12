using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples total (all-cores) CPU utilisation via the Win32 <c>GetSystemTimes</c> API.
/// Each <see cref="Sample"/> call returns the average CPU load as a percentage (0–100)
/// over the interval since the previous call. No dependencies, negligible per-sample cost.
///
/// Shared: the Dashboard and the Processes tab each own an instance (the Processes summary strip
/// shows the same system-wide CPU% as the Dashboard). Moved here from src/Tabs/Dashboard with sign-off
/// when the Processes tab was activated — the same precedent as <c>NetworkUsageSampler</c>.
/// </summary>
public sealed class CpuUsageSampler {
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime {
        public uint LowDateTime;
        public uint HighDateTime;

        public readonly ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;

    public CpuUsageSampler() {
        // Seed an initial snapshot so the very first Sample() reflects a real interval
        // rather than the whole time since boot.
        GetSystemTimes(out var idle, out var kernel, out var user);
        _prevIdle = idle.ToUInt64();
        _prevKernel = kernel.ToUInt64();
        _prevUser = user.ToUInt64();
    }

    /// <summary>
    /// Returns average CPU utilisation (0–100) since the previous call. Kernel time already
    /// includes idle time, so busy = (kernel + user) − idle over the elapsed interval.
    /// </summary>
    public double Sample() {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return 0;

        var idleNow = idle.ToUInt64();
        var kernelNow = kernel.ToUInt64();
        var userNow = user.ToUInt64();

        var idleDelta = idleNow - _prevIdle;
        var totalDelta = (kernelNow - _prevKernel) + (userNow - _prevUser); // kernel includes idle

        _prevIdle = idleNow;
        _prevKernel = kernelNow;
        _prevUser = userNow;

        if (totalDelta == 0)
            return 0;

        var usage = (totalDelta - idleDelta) * 100.0 / totalDelta;

        // Clamp defensively against rounding / turbo edge cases.
        return usage < 0 ? 0 : usage > 100 ? 100 : usage;
    }
}
