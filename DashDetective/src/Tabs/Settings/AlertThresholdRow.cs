using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// One row of the Alerts card: the number a resource is watched against. Whether it is watched at all is
/// not here — that switch is the bell beside the resource on its own page (<c>AlertSwitch</c>), so this
/// number stays editable either way and survives the resource being switched off.
/// </summary>
public sealed partial class AlertThresholdRow : ObservableObject {
    private readonly Action _onChanged;
    private bool _seeding;

    /// <param name="value">The threshold, kept whether or not it is being watched.</param>
    /// <param name="minimum">Lowest value the field will accept.</param>
    /// <param name="maximum">Highest value the field will accept.</param>
    /// <param name="suffix">The unit shown beside the box.</param>
    /// <param name="onChanged">Raised on a real user edit, never while seeding.</param>
    public AlertThresholdRow(int value, int minimum, int maximum, string suffix, Action onChanged) {
        _onChanged = onChanged;
        _seeding = true;

        Minimum = minimum;
        Maximum = maximum;
        Suffix = suffix;
        Value = Math.Clamp(value, minimum, maximum);

        _seeding = false;
    }

    public int Minimum { get; }
    public int Maximum { get; }
    public string Suffix { get; }

    /// <summary>The threshold. Kept while the resource is switched off, so turning it back on restores what
    /// was chosen rather than a default.</summary>
    [ObservableProperty] private int _value;

    partial void OnValueChanged(int value) {
        if (!_seeding)
            _onChanged();
    }
}
