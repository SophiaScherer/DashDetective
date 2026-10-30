using DashDetective.Services.Settings;
using DashDetective.Services.SystemMetrics;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Covers <see cref="ResourceAlertOptions.From"/>, the fold of the persisted switch-and-threshold
/// pairs into the watcher's zero-means-off shape: a switched-off resource reads zero, a switched-on one
/// its own number, and the wait carries over untouched.</summary>
public class ResourceAlertOptionsTests {
    [Fact]
    public void From_TheDefaults_WatchesCpuMemoryAndFreeSpaceOnly() {
        var options = ResourceAlertOptions.From(AppSettings.Defaults);

        Assert.Equal(90, options.CpuPercent);
        Assert.Equal(90, options.MemoryPercent);
        Assert.Equal(0, options.GpuPercent);
        Assert.Equal(0, options.DiskActivePercent);
        Assert.Equal(10, options.LowDiskFreePercent);
        Assert.Equal(10, options.SustainSeconds);
    }

    [Fact]
    public void From_ASwitchedOffResource_ReadsZero_WhateverItsNumber() {
        var options = ResourceAlertOptions.From(AppSettings.Defaults with {
            AlertCpuEnabled = false,
            AlertCpuPercent = 70,
            AlertLowDiskFreeEnabled = false,
        });

        Assert.Equal(0, options.CpuPercent);
        Assert.Equal(0, options.LowDiskFreePercent);
    }

    [Fact]
    public void From_ASwitchedOnResource_UsesItsOwnNumber() {
        var options = ResourceAlertOptions.From(AppSettings.Defaults with {
            AlertGpuEnabled = true,
            AlertGpuPercent = 75,
            AlertDiskActiveEnabled = true,
            AlertDiskActivePercent = 60,
            AlertSustainSeconds = 30,
        });

        Assert.Equal(75, options.GpuPercent);
        Assert.Equal(60, options.DiskActivePercent);
        Assert.Equal(30, options.SustainSeconds);
    }
}
