using System.Collections.ObjectModel;
using DashDetective.Services.Theming;
using DashDetective.Shared;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// Backs the Settings → Appearance section. Owns the Theme and Accent option lists and applies the
/// user's choice through the shared <see cref="ThemeService"/> (the single theming seam). The rest
/// of the Settings page (Monitoring, Export &amp; Data) is still static layout.
/// </summary>
public partial class SettingsViewModel : ViewModelBase {
    private readonly ThemeService _theme;

    public ObservableCollection<ThemeOption> ThemeOptions { get; }
    public ObservableCollection<AccentOption> AccentOptions { get; }

    public SettingsViewModel(ThemeService theme) {
        _theme = theme;

        ThemeOptions = new ObservableCollection<ThemeOption> {
            new("Dark", AppTheme.Dark, SelectTheme),
            new("Light", AppTheme.Light, SelectTheme),
            new("System", AppTheme.System, SelectTheme),
        };

        // The default (multi-colour) option comes first, then the single accents.
        AccentOptions = new ObservableCollection<AccentOption> {
            new(null, SelectAccent),
        };
        foreach (var preset in AccentPreset.All)
            AccentOptions.Add(new AccentOption(preset, SelectAccent));

        // Reflect the service's current selections in the controls.
        foreach (var option in ThemeOptions)
            option.IsSelected = option.Value == _theme.CurrentTheme;
        foreach (var option in AccentOptions)
            option.IsSelected = Equals(option.Preset, _theme.CurrentAccent);
    }

    private void SelectTheme(ThemeOption option) {
        foreach (var other in ThemeOptions)
            other.IsSelected = other == option;
        _theme.ApplyTheme(option.Value);
    }

    private void SelectAccent(AccentOption option) {
        foreach (var other in AccentOptions)
            other.IsSelected = other == option;

        if (option.Preset is { } preset)
            _theme.ApplyAccent(preset);
        else
            _theme.ApplyDefaultAppearance();
    }
}
