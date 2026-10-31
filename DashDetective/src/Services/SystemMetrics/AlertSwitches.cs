using DashDetective.Services.Settings;
using System;
using System.Collections.Generic;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// The five per-resource alert switches, one instance shared by every page that shows a bell. Built once
/// by the shell and handed to the pages, so a flip on Performance and one on Storage are the same state the
/// watcher is fed from — there is no copy for them to disagree with.
/// </summary>
public sealed class AlertSwitches {
    public AlertSwitches() {
        Cpu = new AlertSwitch(AlertMetric.Cpu);
        Memory = new AlertSwitch(AlertMetric.Memory);
        Gpu = new AlertSwitch(AlertMetric.Gpu);
        DiskActivity = new AlertSwitch(AlertMetric.DiskActivity);
        DiskSpace = new AlertSwitch(AlertMetric.DiskSpace);
        All = [Cpu, Memory, Gpu, DiskActivity, DiskSpace];

        foreach (var alert in All)
            alert.Toggled += () => Changed?.Invoke();

        // Seeded from the shipped defaults, so a page built without the shell's instance still shows them.
        Load(AppSettings.Defaults);
        Reflect(AppSettings.Defaults);
    }

    public AlertSwitch Cpu { get; }
    public AlertSwitch Memory { get; }
    public AlertSwitch Gpu { get; }
    public AlertSwitch DiskActivity { get; }
    public AlertSwitch DiskSpace { get; }

    public IReadOnlyList<AlertSwitch> All { get; }

    /// <summary>Raised when the user flips any switch, so the shell can persist and re-arm the watcher.</summary>
    public event Action? Changed;

    /// <summary>Sets every flag from persisted state, silently.</summary>
    public void Load(AppSettings settings) {
        Cpu.Load(settings.AlertCpuEnabled);
        Memory.Load(settings.AlertMemoryEnabled);
        Gpu.Load(settings.AlertGpuEnabled);
        DiskActivity.Load(settings.AlertDiskActiveEnabled);
        DiskSpace.Load(settings.AlertLowDiskFreeEnabled);
    }

    /// <summary>Mirrors what the bells describe — the master switch, each threshold and the wait. Never
    /// touches a flag, so it is safe to call on every settings change.</summary>
    public void Reflect(AppSettings settings) {
        Cpu.Threshold = settings.AlertCpuPercent;
        Memory.Threshold = settings.AlertMemoryPercent;
        Gpu.Threshold = settings.AlertGpuPercent;
        DiskActivity.Threshold = settings.AlertDiskActivePercent;
        DiskSpace.Threshold = settings.AlertLowDiskFreePercent;

        foreach (var alert in All) {
            alert.SustainSeconds = settings.AlertSustainSeconds;
            alert.IsAvailable = settings.ResourceAlerts;
        }
    }

    /// <summary>The snapshot with every flag taken from these switches, for the shell's capture.</summary>
    public AppSettings CaptureInto(AppSettings settings) => settings with {
        AlertCpuEnabled = Cpu.IsOn,
        AlertMemoryEnabled = Memory.IsOn,
        AlertGpuEnabled = Gpu.IsOn,
        AlertDiskActiveEnabled = DiskActivity.IsOn,
        AlertLowDiskFreeEnabled = DiskSpace.IsOn,
    };
}
