using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>One logical processor's utilisation: its platform-native instance name (the PDH "group,core"
/// string on Windows, e.g. "0,3"; the <c>/proc/stat</c> label on Linux, e.g. "cpu3"), the parsed
/// group/core numbers, and the current utilisation percentage. Linux has no processor groups, so its
/// <c>Group</c> is always 0.</summary>
internal readonly record struct LogicalProcessorSample(string Instance, int Group, int Core, double Percent);

/// <summary>
/// A source of per-logical-processor utilisation, one reading per core ordered by (group, core). Drives
/// the Performance tab's CPU "Detailed" view — one mini chart per logical processor. Implementations must
/// never throw: any failure yields an empty set, which leaves the charts unbuilt rather than failing the
/// page.
/// </summary>
internal interface ILogicalProcessorSampler : IDisposable {
    /// <summary>Returns one reading per logical processor at the moment of the call. Empty on any
    /// failure, and empty forever once a sampler has gone inert.</summary>
    IReadOnlyList<LogicalProcessorSample> Sample();

    /// <summary>The reader for this machine — the only place the platform is decided for this seam.</summary>
    static ILogicalProcessorSampler ForCurrentPlatform() {
        if (OperatingSystem.IsWindows())
            return new WindowsLogicalProcessorSampler();

        if (OperatingSystem.IsLinux())
            return new LinuxLogicalProcessorSampler();

        return new UnsupportedLogicalProcessorSampler();
    }
}
