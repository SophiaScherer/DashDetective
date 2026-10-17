using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Whether one resource's alert is on, as the bell beside that resource shows it. The flag is the state;
/// the threshold, the sustain and the master switch are only mirrored here so the bell can say what it
/// would warn about. The shell owns all of them, and is the only thing that writes the mirrors.
/// </summary>
public sealed partial class AlertSwitch : ObservableObject {
    private bool _loading;

    internal AlertSwitch(AlertMetric metric) => Metric = metric;

    /// <summary>Which resource this switch watches.</summary>
    public AlertMetric Metric { get; }

    /// <summary>Whether this resource is watched. A user flip raises <see cref="Toggled"/>; a load does not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private bool _isOn;

    /// <summary>Whether the master "Resource alerts" switch is on. Off disables the bell, since flipping
    /// it then would change nothing anyone could see.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private bool _isAvailable;

    /// <summary>The threshold, in percent, as Settings holds it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private int _threshold;

    /// <summary>How long a usage breach must last. Unused by free space, which has no wait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private int _sustainSeconds;

    /// <summary>Raised when the user flips <see cref="IsOn"/>, never while it is loaded.</summary>
    internal event Action? Toggled;

    /// <summary>The bell's tooltip, and so its accessible name: the state, the rule, and what a click does.</summary>
    public string Description {
        get {
            var state = IsOn ? "on" : "off";
            if (!IsAvailable)
                return $"{Name} {state}. Resource alerts are switched off in Settings.";

            return IsOn
                ? $"{Name} on: warns when {Condition}. Click to turn it off."
                : $"{Name} off. Click to warn when {Condition}.";
        }
    }

    /// <summary>Sets the flag from persisted state without reporting it as an edit.</summary>
    internal void Load(bool isOn) {
        _loading = true;
        IsOn = isOn;
        _loading = false;
    }

    partial void OnIsOnChanged(bool value) {
        if (!_loading)
            Toggled?.Invoke();
    }

    private string Name => Metric switch {
        AlertMetric.Cpu => "CPU alert",
        AlertMetric.Memory => "Memory alert",
        AlertMetric.Gpu => "GPU alert",
        AlertMetric.DiskActivity => "Disk activity alert",
        _ => "Low disk space alert",
    };

    // "Any" for the multi-device metrics: one switch covers every GPU or drive, and the copy says so.
    private string Condition => Metric switch {
        AlertMetric.Cpu => $"CPU use stays at or above {Threshold}% for {Wait}",
        AlertMetric.Memory => $"memory use stays at or above {Threshold}% for {Wait}",
        AlertMetric.Gpu => $"any GPU stays at or above {Threshold}% busy for {Wait}",
        AlertMetric.DiskActivity => $"any drive stays at or above {Threshold}% active for {Wait}",
        _ => $"any drive drops to {Threshold}% free or less",
    };

    private string Wait => SustainSeconds == 1 ? "1 second" : $"{SustainSeconds} seconds";
}
