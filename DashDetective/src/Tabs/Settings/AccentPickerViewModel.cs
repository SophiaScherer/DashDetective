using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using System;
using System.Collections.ObjectModel;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The Accent color row. A draft is picked on the wheel, typed as hex or taken from a preset, previewed in
/// both themes, and reaches the app only through <see cref="ApplyCommand"/>.
/// </summary>
public partial class AccentPickerViewModel : ObservableObject {
    private readonly ThemeService _theme;
    private readonly Action _applied;

    // Set while one side of the wheel/hex pair writes the other, so the write is not read back as an edit.
    private bool _syncing;

    internal AccentPickerViewModel(ThemeService theme, Action applied) {
        _theme = theme;
        _applied = applied;

        Swatches = [];
        foreach (var preset in AccentPreset.All)
            Swatches.Add(new AccentSwatchOption(preset, Select));
        Swatches.Add(new AccentSwatchOption(null, Select));

        _appliedBrush = new SolidColorBrush(Applied.Identity);
        SetDraft(Applied.Identity);

        // Blue is also the field's initial draft, in which case no change fired to paint the swatches.
        OnDraftChanged(Draft);
    }

    /// <summary>The four presets, then Custom.</summary>
    public ObservableCollection<AccentSwatchOption> Swatches { get; }

    /// <summary>The wheel's hue, 0-360.</summary>
    [ObservableProperty] private double _hue;

    [ObservableProperty] private double _saturation;

    [ObservableProperty] private double _brightness = 1;

    /// <summary>The hex box. Taken as typed when it reads as a color; <see cref="ReconcileHex"/> tidies it
    /// when the edit ends.</summary>
    [ObservableProperty] private string _hexText = "";

    /// <summary>What the preview strips render.</summary>
    [ObservableProperty] private AccentPreset _draft = AccentPreset.Default;

    [ObservableProperty] private IBrush _draftBrush = Brushes.Transparent;

    [ObservableProperty] private IBrush _appliedBrush;

    /// <summary>The accent the app is using now.</summary>
    public AccentPreset Applied => _theme.CurrentAccent;

    public bool IsDirty => Draft.Identity != Applied.Identity;

    /// <summary>Opened by any swatch, closed only by Apply, Cancel or Custom with nothing pending. Stored
    /// rather than derived from the draft, so an edit landing back on the applied color cannot close it.</summary>
    [ObservableProperty] private bool _isEditorOpen;

    /// <summary>Where the draft's fill all but disappears, or empty. Text is corrected, so only the fill
    /// is warned about.</summary>
    public string Warning =>
        AccentGuard.IsFaint(Draft.Identity, dark: true) ? "Hard to see on the dark theme"
        : AccentGuard.IsFaint(Draft.Identity, dark: false) ? "Hard to see on the light theme"
        : "";

    public bool HasWarning => Warning.Length > 0;

    /// <summary>The dark strip's variant, following high contrast so the preview matches what applies.</summary>
    public ThemeVariant DarkVariant => _theme.HighContrast ? AppVariants.HighContrastDark : ThemeVariant.Dark;

    public ThemeVariant LightVariant => _theme.HighContrast ? AppVariants.HighContrastLight : ThemeVariant.Light;

    /// <summary>Re-reads high contrast for the strips.</summary>
    internal void RefreshVariants() {
        OnPropertyChanged(nameof(DarkVariant));
        OnPropertyChanged(nameof(LightVariant));
    }

    /// <summary>Puts the draft's hex back in the box, for an edit that ended unreadable or untidy.</summary>
    public void ReconcileHex() => WriteHex(Draft.Identity);

    [RelayCommand]
    private void Apply() {
        if (!IsDirty)
            return;

        _theme.ApplyAccent(Draft);
        IsEditorOpen = false;
        AppliedBrush = new SolidColorBrush(Applied.Identity);
        RaiseDraftState();
        _applied();
    }

    /// <summary>Drops the draft and closes the editor.</summary>
    [RelayCommand]
    private void Cancel() {
        SetDraft(Applied.Identity);
        IsEditorOpen = false;
    }

    /// <summary>A preset drafts itself and opens the editor. Custom toggles it, but will not close over a
    /// pending draft — that is Cancel's job, and a silent discard would lose the pick.</summary>
    private void Select(AccentSwatchOption option) {
        if (option.Preset is { } preset) {
            SetDraft(preset.Identity);
            IsEditorOpen = true;
            return;
        }

        IsEditorOpen = IsDirty || !IsEditorOpen;
    }

    partial void OnHueChanged(double value) => TakeWheel();

    partial void OnSaturationChanged(double value) => TakeWheel();

    partial void OnBrightnessChanged(double value) => TakeWheel();

    private void TakeWheel() {
        if (_syncing)
            return;

        var color = HsvColor.ToRgb(ColorPickerGeometry.NormalizeHue(Hue),
                                   Math.Clamp(Saturation, 0, 1), Math.Clamp(Brightness, 0, 1));
        WriteHex(color);
        Draft = AccentPreset.FromIdentity(color);
    }

    partial void OnHexTextChanged(string value) {
        if (_syncing || !AccentPreset.TryParseHex(value, out var color))
            return;

        WriteWheel(color);
        Draft = AccentPreset.FromIdentity(color);
    }

    private void SetDraft(Color color) {
        WriteWheel(color);
        WriteHex(color);
        Draft = AccentPreset.FromIdentity(color);
    }

    /// <summary>Moves the wheel to a color. Gray has no hue and black no saturation either, so those keep
    /// where the wheel already was.</summary>
    private void WriteWheel(Color color) {
        var hsv = color.ToHsv();
        _syncing = true;
        if (hsv.V > 0 && hsv.S > 0)
            Hue = hsv.H;
        if (hsv.V > 0)
            Saturation = hsv.S;
        Brightness = hsv.V;
        _syncing = false;
    }

    private void WriteHex(Color color) {
        _syncing = true;
        HexText = AccentPreset.FromIdentity(color).Hex;
        _syncing = false;
    }

    partial void OnDraftChanged(AccentPreset value) {
        DraftBrush = new SolidColorBrush(value.Identity);
        foreach (var swatch in Swatches)
            swatch.IsSelected = swatch.Preset is { } preset
                ? preset.Identity == value.Identity
                : value.Name == AccentPreset.CustomName;
        RaiseDraftState();
    }

    private void RaiseDraftState() {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(HasWarning));
    }
}
