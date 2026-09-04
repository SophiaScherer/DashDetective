using DashDetective.Services.Settings;
using DashDetective.Services.Theming;
using System;

namespace DashDetective.Services.Accessibility;

/// <summary>
/// The state behind Settings → Accessibility, and the one place it is applied. It writes nothing to the
/// application itself — appearance goes through <see cref="ThemeService"/>, which stays the only code
/// that touches <c>Application.Current</c>.
///
/// It exists so the card's options have a single owner: that is what "Restore defaults" resets, and what
/// the shell reads to size its minimum against the current scale.
/// </summary>
internal sealed class AccessibilityService {
    private readonly ThemeService _theme;

    internal AccessibilityService(ThemeService theme) => _theme = theme;

    /// <summary>The chosen UI scale, always one of <see cref="UiScale.Percents"/>.</summary>
    internal int ScalePercent { get; private set; } = UiScale.DefaultPercent;

    /// <summary>The same value as a transform factor, for <c>ScaleHost</c> and the window minimum.</summary>
    internal double ScaleFactor => UiScale.Factor(ScalePercent);

    /// <summary>Whether high contrast is in force. Off by default: it changes what the app looks like.</summary>
    internal bool HighContrast { get; private set; }

    /// <summary>Raised when an option actually changes, so the shell can resize and persist.</summary>
    internal event Action? Changed;

    /// <summary>Applies the persisted state, at startup and before the Settings page is built.</summary>
    internal void Apply(AppSettings settings) {
        SetScalePercent(settings.UiScalePercent);
        SetHighContrast(settings.HighContrast);
    }

    /// <summary>Puts every option on the card back to what it ships as.</summary>
    internal void RestoreDefaults() {
        SetScalePercent(UiScale.DefaultPercent);
        SetHighContrast(false);
    }

    /// <summary>Selects a scale. Re-applying the current one is deliberate — startup has to push the
    /// value through whether or not it differs — but only a real change is announced.</summary>
    internal void SetScalePercent(int percent) {
        var next = UiScale.Nearest(percent);
        var changed = next != ScalePercent;

        ScalePercent = next;
        _theme.ApplyUiScale(ScaleFactor, UiScale.PopupFontSize(ScalePercent));

        if (changed)
            Changed?.Invoke();
    }

    /// <summary>Turns high contrast on or off. Re-applies unconditionally and announces only a real
    /// change, for the same reason <see cref="SetScalePercent"/> does.</summary>
    internal void SetHighContrast(bool enabled) {
        var changed = enabled != HighContrast;

        HighContrast = enabled;
        _theme.ApplyContrast(enabled);

        if (changed)
            Changed?.Invoke();
    }
}
