using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// One row of the Alerts card: whether the resource is watched, and the number to watch it against.
///
/// The two are kept apart deliberately. The settings layer encodes "not watched" as a threshold of zero,
/// but a row that stored only that would forget the number as soon as it was switched off — so GPU could
/// not ship "off, and defaulted to 90", and re-enabling a row would land on a default rather than on
/// whatever was chosen before. The shell folds the pair back into the service's zero-means-off contract.
/// </summary>
public sealed partial class AlertThresholdRow : ObservableObject {
    private readonly Action _onChanged;
    private bool _seeding;

    /// <param name="isEnabled">Whether the resource is watched.</param>
    /// <param name="value">The threshold, kept whether or not it is being watched.</param>
    /// <param name="minimum">Lowest value the field will accept.</param>
    /// <param name="maximum">Highest value the field will accept.</param>
    /// <param name="suffix">The unit shown beside the box.</param>
    /// <param name="onChanged">Raised on a real user edit, never while seeding.</param>
    public AlertThresholdRow(bool isEnabled, int value, int minimum, int maximum, string suffix,
                             Action onChanged) {
        _onChanged = onChanged;
        _seeding = true;

        Minimum = minimum;
        Maximum = maximum;
        Suffix = suffix;
        IsEnabled = isEnabled;
        Value = Math.Clamp(value, minimum, maximum);

        _seeding = false;
    }

    public int Minimum { get; }
    public int Maximum { get; }
    public string Suffix { get; }

    /// <summary>Whether this resource is watched at all. Drives the row's toggle, and dims the field.</summary>
    [ObservableProperty] private bool _isEnabled;

    /// <summary>The threshold. Kept while the row is switched off, so turning it back on restores what
    /// was chosen rather than a default.</summary>
    [ObservableProperty] private int _value;

    /// <summary>What the settings layer stores: the threshold, or zero for a row that is not watched.</summary>
    public int EffectiveValue => IsEnabled ? Value : 0;

    partial void OnIsEnabledChanged(bool value) => Raise();

    partial void OnValueChanged(int value) => Raise();

    private void Raise() {
        if (!_seeding)
            _onChanged();
    }
}
