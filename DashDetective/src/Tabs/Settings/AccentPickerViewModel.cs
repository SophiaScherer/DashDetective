using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashDetective.Services.Theming;
using System;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// The accent picker modal. A draft is picked on the wheel, typed as hex or reverted to Blue, previewed in
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
        _appliedBrush = new SolidColorBrush(Applied.Identity);
        SetDraft(Applied.Identity);

        // Blue is also the field's initial draft, in which case no change fired to paint the brush.
        OnDraftChanged(Draft);
    }

    /// <summary>Raised when the modal closes, so the opener can take focus back.</summary>
    public event Action? Closed;

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

    /// <summary>The applied accent's fill, for the Settings row and the before chip.</summary>
    [ObservableProperty] private IBrush _appliedBrush;

    /// <summary>Whether the modal is up. Stored rather than derived from the draft, so an edit landing back
    /// on the applied color cannot close it.</summary>
    [ObservableProperty] private bool _isOpen;

    /// <summary>The accent the app is using now.</summary>
    public AccentPreset Applied => _theme.CurrentAccent;

    public bool IsDirty => Draft.Identity != Applied.Identity;

    /// <summary>Whether reverting would change the draft.</summary>
    public bool CanRevert => Draft != AccentPreset.Default;

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

    /// <summary>Opens on the applied accent. Already open, it does nothing, so a pending pick survives.</summary>
    [RelayCommand]
    private void Open() {
        if (IsOpen)
            return;

        SetDraft(Applied.Identity);
        IsOpen = true;
    }

    [RelayCommand]
    private void Apply() {
        if (!IsDirty)
            return;

        _theme.ApplyAccent(Draft);
        AppliedBrush = new SolidColorBrush(Applied.Identity);
        RaiseDraftState();
        Close();
        _applied();
    }

    /// <summary>Drops the draft and closes. Esc routes here too.</summary>
    [RelayCommand]
    private void Cancel() {
        SetDraft(Applied.Identity);
        Close();
    }

    /// <summary>Drafts Blue; Apply still commits it.</summary>
    [RelayCommand]
    private void RevertToDefault() => SetDraft(AccentPreset.Default.Identity);

    /// <summary>A press on the dim backdrop. Closes only with nothing pending, so a stray click cannot lose
    /// a pick.</summary>
    public void DismissFromBackdrop() {
        if (!IsDirty)
            Close();
    }

    private void Close() {
        if (!IsOpen)
            return;

        IsOpen = false;
        Closed?.Invoke();
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
        RaiseDraftState();
    }

    private void RaiseDraftState() {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanRevert));
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(HasWarning));
    }
}
