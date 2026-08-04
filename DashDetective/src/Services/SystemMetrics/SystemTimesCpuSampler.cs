using System;
using System.Runtime.InteropServices;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Idle-based CPU sampler via the Win32 <c>GetSystemTimes</c> API — the "% Processor Time" method.
/// Used as the fallback when the frequency-normalised PDH "% Processor Utility" counter (which Task
/// Manager uses, see <see cref="ProcessorUtilityCpuSampler"/>) can't be created. No dependencies,
/// negligible per-sample cost. Being the last CPU fallback, a <c>kernel32</c> load failure leaves it
/// inert (returning 0 forever) rather than throwing — there is nothing further to fall back to.
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

    /// <summary>Whether <c>GetSystemTimes</c> is usable at all; when false <see cref="Sample"/> returns 0
    /// without touching the native call.</summary>
    internal bool Ready { get; }

    public SystemTimesCpuSampler() {
        // Seed an initial snapshot so the very first Sample() reflects a real interval
        // rather than the whole time since boot.
        try {
            GetSystemTimes(out var idle, out var kernel, out var user);
            _prevIdle = idle.ToUInt64();
            _prevKernel = kernel.ToUInt64();
            _prevUser = user.ToUInt64();
            Ready = true;
        } catch (Exception ex) when (NativeLoadFailure.Matches(ex)) {
            NativeLoadFailure.Report(nameof(SystemTimesCpuSampler), ex);
        }
    }

    /// <summary>Test seam: skips native initialisation so the inert soft-fail contract can be exercised on
    /// a healthy host, where the real constructor always succeeds.</summary>
    internal SystemTimesCpuSampler(SamplerInit _) { }

    /// <summary>
    /// Returns average CPU utilisation (0–100) since the previous call. Kernel time already
    /// includes idle time, so busy = (kernel + user) − idle over the elapsed interval. Yields 0 when the
    /// sampler is inert — the Ready check short-circuits before the native call, so an unusable
    /// <c>kernel32</c> can't throw once per tick.
    /// </summary>
    public double Sample() {
        if (!Ready || !GetSystemTimes(out var idle, out var kernel, out var user))
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
