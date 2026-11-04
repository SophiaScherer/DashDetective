using DashDetective.Services.Diagnostics;
using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Samples total (all-cores) CPU utilisation as a percentage (0–100). On Windows it prefers the PDH
/// "% Processor Utility" counter (<see cref="ProcessorUtilityCpuSampler"/>) so the reading matches
/// Task Manager, and falls back to the idle-based <c>GetSystemTimes</c> method
/// (<see cref="SystemTimesCpuSampler"/>) when that counter can't be created; on Linux it reads
/// <c>/proc/stat</c> (<see cref="LinuxCpuSampler"/>). Each <see cref="Sample"/> call returns the load
/// over the interval since the previous call.
///
/// Shared: the Dashboard and the Processes tab each own an instance (the Processes summary strip
/// shows the same system-wide CPU% as the Dashboard). Moved here from src/Tabs/Dashboard with sign-off
/// when the Processes tab was activated — the same precedent as <c>NetworkUsageSampler</c>.
/// </summary>
public sealed class CpuUsageSampler : IDisposable {
    private ICpuSampler _inner;
    private ICpuSampler? _fallback;

    /// <summary>Picks the reader for this platform — the only place that choice is made. The Linux arm
    /// comes first so the Windows chain below sits behind an <c>IsWindows</c> guard, which is what CA1416
    /// needs to see for the two PDH constructors.</summary>
    public CpuUsageSampler() {
        if (OperatingSystem.IsLinux()) {
            _inner = new LinuxCpuSampler();
            return;
        }

        if (!OperatingSystem.IsWindows()) {
            _inner = new UnsupportedCpuSampler();
            return;
        }

        // Prefer the Task-Manager-matching counter; fall back to GetSystemTimes if it isn't available.
        var utility = new ProcessorUtilityCpuSampler();
        var fallback = new SystemTimesCpuSampler();
        _inner = UtilityIfReady(utility, utility.Ready) ?? fallback;
        // Kept (and seeded) only while the utility counter is in use, for a runtime read failure.
        _fallback = ReferenceEquals(_inner, fallback) ? null : fallback;
    }

    /// <summary>The utility counter when it stood up; otherwise disposes it and returns null so the caller
    /// takes the fallback. Split out so the choice is testable on every platform.</summary>
    internal static ICpuSampler? UtilityIfReady(ICpuSampler utility, bool ready) {
        if (ready)
            return utility;
        (utility as IDisposable)?.Dispose();
        return null;
    }

    /// <summary>Test seam: injects the underlying sampler so fallback selection can be exercised
    /// headlessly without real hardware counters.</summary>
    internal CpuUsageSampler(ICpuSampler inner) => _inner = inner;

    /// <summary>Test seam: injects a primary sampler plus the one to switch to if it throws at runtime.</summary>
    internal CpuUsageSampler(ICpuSampler inner, ICpuSampler? fallback) {
        _inner = inner;
        _fallback = fallback;
    }

    /// <summary>Test seam: the reader the public constructor chose, so the platform dispatch can be
    /// asserted on whichever CI leg is running.</summary>
    internal ICpuSampler Inner => _inner;

    /// <summary>Returns CPU utilisation (0–100) for the interval since the previous call.</summary>
    public double Sample() {
        try {
            return _inner.Sample();
        } catch (Exception ex) when (_fallback is not null && ex is InvalidOperationException) {
            // One failed counter read must not blank the CPU readout for the session (MetricChannel stops
            // polling after a throw), so switch to the fallback; if that also throws, it propagates.
            Log.Warn("CPU utility counter read failed; switching to GetSystemTimes", ex);
            var failed = _inner;
            _inner = _fallback;
            _fallback = null;
            (failed as IDisposable)?.Dispose();
            return _inner.Sample();
        }
    }

    /// <summary>Releases the PDH query handle when the utility counter is in use. Safe to call twice.</summary>
    public void Dispose() => (_inner as IDisposable)?.Dispose();
}
