using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Services.SystemMetrics;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// One metric card in the Dashboard's top stat row, rendered by the shared <c>StatCard</c> control through
/// an <c>ItemsControl</c>. Carries the card's <see cref="Category"/> (which selects its theme-aware accent
/// brush via the <c>Classes.*</c> bindings in the template) plus the live <see cref="Value"/> /
/// <see cref="Unit"/> / <see cref="Sub"/> / <see cref="Points"/> the owning <see cref="DashboardViewModel"/>
/// updates in place each tick.
///
/// The collection is the multi-instance seam: one card per detected device, so several disks (or, later,
/// several GPUs/CPUs) each get their own card grouped with their kind.
/// </summary>
public partial class DashboardCard : ObservableObject {
    public DashboardCard(DeviceCategory category, string label, string unit) {
        Category = category;
        Label = label;
        _unit = unit;
    }

    public DeviceCategory Category { get; }

    /// <summary>Uppercase card heading (e.g. "CPU", "LOCAL DISK (C:)").</summary>
    public string Label { get; }

    [ObservableProperty] private string _value = "0";
    [ObservableProperty] private string _unit;
    [ObservableProperty] private string _sub = "";
    [ObservableProperty] private string _points = "";

    /// <summary>Top of this card's chart axis. Most cards plot a percentage and keep the default; a card
    /// whose series is auto-scaled (throughput) rewrites it each tick from its own live ceiling, so the
    /// label cannot claim a percentage the chart isn't drawn on.</summary>
    [ObservableProperty] private string _axisMaxLabel = "100%";

    /// <summary>Why this card shows "—" rather than a value, or "" when it has one. The card has no room for
    /// a line of its own, so the template hangs it off the tooltip.</summary>
    [ObservableProperty] private string _note = "";

    /// <summary><see cref="Note"/> as a tooltip, or null when there is nothing to say. Avalonia only skips
    /// a tooltip on null, so binding the bare string pops an empty one on every healthy card.</summary>
    public string? NoteTip => Note.Length > 0 ? Note : null;

    partial void OnNoteChanged(string value) => OnPropertyChanged(nameof(NoteTip));

    // Category flags the StatCard template binds to Classes.* so each card picks up its semantic accent brush
    // (ChartCpu / ChartMemory / …) via style setters, keeping the accents theme/accent-aware.
    public bool IsCpu => Category == DeviceCategory.Cpu;
    public bool IsMemory => Category == DeviceCategory.Memory;
    public bool IsGpu => Category == DeviceCategory.Gpu;
    public bool IsDisk => Category == DeviceCategory.Disk;
    public bool IsNetwork => Category == DeviceCategory.Network;
}
