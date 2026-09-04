using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.SystemMetrics;
using System;
using System.Linq;
using System.Windows.Input;

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
    /// <summary>What a card's tooltip says when it has no note of its own.</summary>
    public const string OpenHint = "Open in Performance";

    public DashboardCard(
        DeviceCategory category, string deviceId, string label, string unit, Action<string> onOpen) {
        Category = category;
        DeviceId = deviceId;
        Label = label;
        _unit = unit;
        OpenCommand = new RelayCommand(() => onOpen(deviceId));
    }

    public DeviceCategory Category { get; }

    /// <summary>The inventory id of the device this card shows, which is how the Performance tab is asked
    /// for it (<c>PerformanceViewModel.Reveal</c>).</summary>
    public string DeviceId { get; }

    /// <summary>Opens this card's device on the Performance tab. The <c>PageLink</c> shape: the item holds
    /// the command and calls back into the view model that owns it.</summary>
    public ICommand OpenCommand { get; }

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

    /// <summary>The card's tooltip: its note when it has one, otherwise where a click goes. A card has no
    /// room for a line of its own, so both hang off the tooltip and the note wins — a card explaining why it
    /// reads "—" has more to say than one repeating an affordance.</summary>
    public string Tip => Note.Length > 0 ? Note : OpenHint;

    /// <summary>What a screen reader announces. The tooltip alone would read "Open in Performance" on
    /// every card, so this leads with the heading and the reading.</summary>
    public string AccessibleName => Note.Length > 0
        ? $"{Label}, {Note}"
        : string.Join(", ", new[] { Label, $"{Value} {Unit}".Trim(), Sub }.Where(p => p.Length > 0));

    partial void OnNoteChanged(string value) {
        OnPropertyChanged(nameof(Tip));
        OnPropertyChanged(nameof(AccessibleName));
    }

    partial void OnValueChanged(string value) => OnPropertyChanged(nameof(AccessibleName));

    partial void OnUnitChanged(string value) => OnPropertyChanged(nameof(AccessibleName));

    partial void OnSubChanged(string value) => OnPropertyChanged(nameof(AccessibleName));

    // Category flags the StatCard template binds to Classes.* so each card picks up its semantic accent brush
    // (ChartCpu / ChartMemory / …) via style setters, keeping the accents theme/accent-aware.
    public bool IsCpu => Category == DeviceCategory.Cpu;
    public bool IsMemory => Category == DeviceCategory.Memory;
    public bool IsGpu => Category == DeviceCategory.Gpu;
    public bool IsDisk => Category == DeviceCategory.Disk;
    public bool IsNetwork => Category == DeviceCategory.Network;
}
