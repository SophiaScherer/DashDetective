using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// A selectable entry in the refresh-interval segmented control (0.5s / 1s / 2s / 5s). Mirrors
/// <see cref="ThemeOption"/>: an observable <see cref="IsSelected"/> flag plus a command that reports
/// the click back to the owning view-model, which retimes the metric channels.
/// </summary>
public partial class IntervalOption : ObservableObject {
    public IntervalOption(string label, double seconds, Action<IntervalOption> onSelected) {
        Label = label;
        Seconds = seconds;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }

    /// <summary>The interval in seconds applied to the live-metric channels.</summary>
    public double Seconds { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
