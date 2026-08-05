using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// One clock reading, in whichever form the platform can supply. Windows sets only
/// <paramref name="PercentOfBase"/> — PDH reports the current clock as a ratio of the base clock, which
/// the caller multiplies by the base clock from the static CPU info. Linux sets only
/// <paramref name="AbsoluteMhz"/>, because <c>cpufreq</c> and <c>/proc/cpuinfo</c> report the clock
/// directly and there is no dependable base clock to divide by. Both zero means "no reading".
/// </summary>
internal readonly record struct ProcessorClockSample(double PercentOfBase, double AbsoluteMhz);

/// <summary>
/// A source of the CPU's current clock, for the Performance tab's "Speed" tile. Page-local: the shared CPU
/// feed carries only the clamped utilisation figure. Implementations must never throw — any failure yields
/// a default sample, which the caller renders as "—".
/// </summary>
internal interface IProcessorFrequencySampler : IDisposable {
    /// <summary>Returns the current clock, or <c>default</c> when no source is readable.</summary>
    ProcessorClockSample Sample();

    /// <summary>The reader for this machine — the only place the platform is decided for this seam.</summary>
    static IProcessorFrequencySampler ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsProcessorFrequencySampler();

        if (OperatingSystem.IsLinux())
            return new LinuxProcessorFrequencySampler();

        return new UnsupportedProcessorFrequencySampler();
    }
}
