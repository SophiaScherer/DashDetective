using DashDetective.Services.Platform.Linux;
using System;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// The CPU's current clock on Linux, in absolute MHz. Prefers <c>cpufreq</c>'s per-core
/// <c>scaling_cur_freq</c> (kHz, the live governor reading), averaged across the online cores the way the
/// Windows counter's <c>_Total</c> instance does. Falls back to <c>/proc/cpuinfo</c>'s <c>cpu MHz</c>
/// lines, which is what a virtual machine usually has — <c>cpufreq</c> is typically absent under
/// VirtualBox, since the guest does not control the clock.
///
/// Stateless and never throws: with neither source readable it returns a default sample and the Speed
/// tile keeps its placeholder.
/// </summary>
internal sealed class LinuxProcessorFrequencySampler : IProcessorFrequencySampler {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string CpuRoot = "/sys/devices/system/cpu";
    private const string CpuInfoPath = "/proc/cpuinfo";
    private const string CpuPrefix = "cpu";
    private const string CpuMhzKey = "cpu MHz";

    private readonly IProcFileSystem _proc;

    public LinuxProcessorFrequencySampler() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so both sources — and the fall-through between them —
    /// can be exercised against canned fixtures from any dev machine.</summary>
    internal LinuxProcessorFrequencySampler(IProcFileSystem proc) => _proc = proc;

    /// <summary>Returns the mean current clock in MHz, or <c>default</c> when neither source yields a
    /// reading.</summary>
    public ProcessorClockSample Sample() {
        var mhz = ReadScalingFrequency();
        if (mhz <= 0)
            mhz = ReadCpuInfoFrequency();

        // Linux reports the clock directly, so there is no ratio to give — see ProcessorClockSample.
        return mhz > 0 ? new ProcessorClockSample(PercentOfBase: 0, mhz) : default;
    }

    /// <summary>Nothing to release — the seam is <c>IDisposable</c> for the PDH arm's sake.</summary>
    public void Dispose() { }

    /// <summary>The mean of every online core's <c>scaling_cur_freq</c>, converted from kHz. Returns 0 when
    /// <c>cpufreq</c> is absent, which is the usual case in a VM.</summary>
    private double ReadScalingFrequency() {
        double sum = 0;
        var count = 0;

        foreach (var entry in _proc.ListDirectory(CpuRoot)) {
            if (!entry.StartsWith(CpuPrefix, StringComparison.Ordinal)
                || !int.TryParse(entry.AsSpan(CpuPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                continue;

            var text = _proc.ReadAllText(CpuRoot + "/" + entry + "/cpufreq/scaling_cur_freq");
            if (text is null
                || !double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var kHz)
                || kHz <= 0)
                continue;

            sum += kHz / 1000.0;
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    /// <summary>The mean of every processor block's <c>cpu MHz</c>. Reads through
    /// <see cref="ProcCpuinfoParser"/> so the file's tab separation and block layout are known in one
    /// place rather than re-derived per consumer.</summary>
    private double ReadCpuInfoFrequency() {
        double sum = 0;
        var count = 0;

        foreach (var block in ProcCpuinfoParser.Parse(_proc.ReadAllLines(CpuInfoPath))) {
            if (!double.TryParse(
                    ProcCpuinfoParser.Value(block, CpuMhzKey),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz)
                || mhz <= 0)
                continue;

            sum += mhz;
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }
}
