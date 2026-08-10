using DashDetective.Tabs.Processes;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="ProcessGroupNames"/>: the third group reads "Windows processes" on Windows
/// and "System processes" on Linux, and the explicit parameter is what lets both arms be checked from
/// either host.</summary>
public class ProcessGroupNamesTests {
    /// <summary>The Windows-no-op half of the milestone: these two strings are exactly what the tab showed
    /// before the Linux arm existed, so a Windows user sees no change at all.</summary>
    [Fact]
    public void ForWindows_IsUnchangedFromTheOriginalWording() {
        Assert.Equal("Windows processes", ProcessGroupNames.HeaderFor(linux: false));
        Assert.Equal("Windows", ProcessGroupNames.LabelFor(linux: false));
    }

    /// <summary>"Windows processes · 150" on a Linux desktop reads as a bug, not as a category.</summary>
    [Fact]
    public void ForLinux_SaysSystemInstead() {
        Assert.Equal("System processes", ProcessGroupNames.HeaderFor(linux: true));
        Assert.Equal("System", ProcessGroupNames.LabelFor(linux: true));
    }

    /// <summary>The host-resolved values are the parameterised ones, so nothing can drift between the two
    /// entry points.</summary>
    [Fact]
    public void HostValues_MatchThisPlatformsArm() {
        var linux = OperatingSystem.IsLinux();

        Assert.Equal(ProcessGroupNames.HeaderFor(linux), ProcessGroupNames.SystemHeader);
        Assert.Equal(ProcessGroupNames.LabelFor(linux), ProcessGroupNames.SystemLabel);
    }
}
