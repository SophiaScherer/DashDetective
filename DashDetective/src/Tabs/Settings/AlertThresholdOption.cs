using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// One choice in an alert-threshold segmented control. Mirrors <see cref="IntervalOption"/>, except that
/// the owning <see cref="AlertThresholdRow"/> handles selection rather than the page view-model — there
/// are six of these rows, and each has to clear only its own segments.
/// </summary>
public partial class AlertThresholdOption : ObservableObject {
    public AlertThresholdOption(string label, int value, Action<AlertThresholdOption> onSelected) {
        Label = label;
        Value = value;
        SelectCommand = new RelayCommand(() => onSelected(this));
    }

    public string Label { get; }

    /// <summary>The threshold as a percentage (or seconds, for the sustain row). Zero means the metric
    /// is not watched.</summary>
    public int Value { get; }

    public ICommand SelectCommand { get; }

    [ObservableProperty] private bool _isSelected;
}
