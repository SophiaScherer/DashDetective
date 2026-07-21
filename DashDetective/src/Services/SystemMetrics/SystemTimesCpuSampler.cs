using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Idle-based CPU sampler via the Win32 <c>GetSystemTimes</c> API — the "% Processor Time" method.
/// Used as the fallback when the frequency-normalised PDH "% Processor Utility" counter (which Task
/// Manager uses, see <see cref="ProcessorUtilityCpuSampler"/>) can't be created. No dependencies,
/// negligible per-sample cost.
/// </summary>
internal sealed class SystemTimesCpuSampler : ICpuSampler {
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

    public SystemTimesCpuSampler() {
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

        return ComputeUsage(idleDelta, totalDelta);
    }

    /// <summary>Pure busy-fraction math, split out so it can be unit-tested with injected deltas.
    /// Returns 0 for an empty interval and clamps defensively against rounding / turbo edge cases.</summary>
    internal static double ComputeUsage(ulong idleDelta, ulong totalDelta) {
        if (totalDelta == 0)
            return 0;

        var usage = (totalDelta - idleDelta) * 100.0 / totalDelta;
        return usage < 0 ? 0 : usage > 100 ? 100 : usage;
    }
}
