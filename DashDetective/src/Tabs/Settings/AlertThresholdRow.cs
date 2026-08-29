using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// One row of the Alerts card: its segmented choices, which is selected, and the value that selection
/// means. Six rows share this rather than six collections and six near-identical handlers on
/// <see cref="SettingsViewModel"/> — a row has to clear only its own segments, so selection belongs with
/// the collection it clears.
/// </summary>
public sealed class AlertThresholdRow : ObservableObject {
    private readonly Action _onChanged;

    /// <param name="choices">The segments, in display order.</param>
    /// <param name="selected">The persisted value; an unknown one falls back to the first choice.</param>
    /// <param name="onChanged">Raised on a real user selection, never while seeding.</param>
    public AlertThresholdRow((string Label, int Value)[] choices, int selected, Action onChanged) {
        _onChanged = onChanged;

        Options = [];
        foreach (var (label, value) in choices)
            Options.Add(new AlertThresholdOption(label, value, Select));

        Seed(selected);
    }

    public ObservableCollection<AlertThresholdOption> Options { get; }

    /// <summary>The selected value, for capturing into settings.</summary>
    public int Value { get; private set; }

    private void Seed(int selected) {
        foreach (var option in Options)
            if (option.Value == selected) {
                Apply(option);
                return;
            }

        Apply(Options[0]);
    }

    private void Select(AlertThresholdOption option) {
        if (option.IsSelected)
            return;

        Apply(option);
        _onChanged();
    }

    private void Apply(AlertThresholdOption option) {
        foreach (var other in Options)
            other.IsSelected = other == option;
        Value = option.Value;
    }
}
