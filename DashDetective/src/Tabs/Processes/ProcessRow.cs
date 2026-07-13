using System;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// A row VM for the process table. Its identity is the <see cref="Pid"/>; the metric fields are
/// observable and updated in place via <see cref="Update"/> so the live list doesn't flicker (the
/// keyed-diff pattern from <c>ConnectionRow</c>). Status colours are fixed semantic colours (green =
/// Running, amber = otherwise), not themed — matching the app's other status indicators. CPU
/// elevation is exposed as <see cref="CpuHigh"/>/<see cref="CpuMed"/> booleans so the view can tint
/// the cell via style classes while the normal value keeps following the theme text ramp.
///
/// Disk and GPU arrive in a later phase; Network is deferred, so those cells render "—" for now.
/// </summary>
public partial class ProcessRow : ObservableObject {
    private static readonly IBrush RunningBrush = new SolidColorBrush(Color.Parse("#6ccb5f"));
    private static readonly IBrush WarnBrush = new SolidColorBrush(Color.Parse("#ffcf4d"));

    public ProcessRow(ProcessInfo info) {
        Pid = info.Pid;
        PidText = info.Pid.ToString(CultureInfo.InvariantCulture);
        Category = info.Category;
        _name = info.Name;
        _status = info.Status;
        _statusBrush = BrushFor(info.Status);
        _cpuText = FormatCpu(info.CpuPercent);
        _cpuHigh = info.CpuPercent > 10;
        _cpuMed = info.CpuPercent is > 5 and <= 10;
        _memoryText = ProcessMemoryFormatter.Format(info.MemoryBytes);
        _diskText = FormatDisk(info.DiskBytesPerSec);
        _gpuText = FormatGpu(info.GpuPercent);
    }

    /// <summary>Stable identity for the keyed diff (unique among live processes).</summary>
    public int Pid { get; }
    public string PidText { get; }

    /// <summary>The group this row lives in. A process that changes category is handled as a
    /// remove-from-one-group + add-to-the-other by the per-group diff, so this stays fixed.</summary>
    public ProcessCategory Category { get; }

    /// <summary>Image name. Observable only to stay correct across the rare PID reuse.</summary>
    [ObservableProperty] private string _name;

    [ObservableProperty] private string _status;
    [ObservableProperty] private IBrush _statusBrush;
    [ObservableProperty] private string _cpuText;
    [ObservableProperty] private bool _cpuHigh;
    [ObservableProperty] private bool _cpuMed;
    [ObservableProperty] private string _memoryText;
    [ObservableProperty] private string _diskText;
    [ObservableProperty] private string _gpuText;

    /// <summary>Whether this row is the current selection. Selection state, not process data, so the
    /// keyed-diff <see cref="Update"/> deliberately leaves it untouched across polls.</summary>
    [ObservableProperty] private bool _isSelected;

    // Per-process Network throughput is deferred (no in-box per-process rate API).
    public string NetworkText => "—";

    /// <summary>Refreshes the mutable fields from a newer snapshot of the same process.</summary>
    public void Update(ProcessInfo info) {
        Name = info.Name;
        Status = info.Status;
        StatusBrush = BrushFor(info.Status);
        CpuText = FormatCpu(info.CpuPercent);
        CpuHigh = info.CpuPercent > 10;
        CpuMed = info.CpuPercent is > 5 and <= 10;
        MemoryText = ProcessMemoryFormatter.Format(info.MemoryBytes);
        DiskText = FormatDisk(info.DiskBytesPerSec);
        GpuText = FormatGpu(info.GpuPercent);
    }

    private static string FormatCpu(double percent) {
        if (percent < 0)
            percent = 0;
        return percent.ToString("F1", CultureInfo.InvariantCulture) + "%";
    }

    // Disk rate → MB/s (one decimal); a bare "0" for idle (matching the design comp) keeps the column
    // quiet when most processes aren't touching the disk.
    private static string FormatDisk(double bytesPerSec) {
        var mbps = bytesPerSec / (1024d * 1024d);
        return mbps < 0.05 ? "0" : mbps.ToString("F1", CultureInfo.InvariantCulture) + " MB/s";
    }

    // GPU → whole percent (Task Manager rounds the per-process GPU figure).
    private static string FormatGpu(double percent) {
        if (percent < 0)
            percent = 0;
        return Math.Round(percent).ToString(CultureInfo.InvariantCulture) + "%";
    }

    private static IBrush BrushFor(string status) =>
        status == "Running" ? RunningBrush : WarnBrush;
}
