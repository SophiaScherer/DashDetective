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

    /// <summary>Whether a two-series chart dashes its second line. Off by default: it is a visible
    /// change to every chart that draws two series.</summary>
    internal bool DistinguishWithoutColor { get; private set; }

    /// <summary>The colour-vision mode. None by default: it changes chart and status colours.</summary>
    internal ColorVisionMode ColorVision { get; private set; }

    /// <summary>Whether the two banners announce themselves to a screen reader. On by default: a
    /// resource alert is the app's one unprompted warning, and silence would hide it.</summary>
    internal bool AnnounceUpdates { get; private set; } = true;

    /// <summary>Raised when an option actually changes, so the shell can resize and persist.</summary>
    internal event Action? Changed;

    /// <summary>Applies the persisted state, at startup and before the Settings page is built.</summary>
    internal void Apply(AppSettings settings) {
        SetScalePercent(settings.UiScalePercent);
        SetHighContrast(settings.HighContrast);
        SetDistinguishWithoutColor(settings.DistinguishWithoutColor);
        SetColorVision(settings.ColorVision);
        SetAnnounceUpdates(settings.AnnounceUpdates);
    }

    /// <summary>Puts every option on the card back to what it ships as.</summary>
    internal void RestoreDefaults() {
        SetScalePercent(UiScale.DefaultPercent);
        SetHighContrast(false);
        SetDistinguishWithoutColor(false);
        SetColorVision(ColorVisionMode.None);
        SetAnnounceUpdates(true);
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

    /// <summary>Turns chart patterns on or off, on the same terms as the two above.</summary>
    internal void SetDistinguishWithoutColor(bool enabled) {
        var changed = enabled != DistinguishWithoutColor;

        DistinguishWithoutColor = enabled;
        _theme.ApplyChartPatterns(enabled);

        if (changed)
            Changed?.Invoke();
    }

    /// <summary>Selects a colour-vision mode, on the same terms as the options above.</summary>
    internal void SetColorVision(ColorVisionMode mode) {
        var changed = mode != ColorVision;

        ColorVision = mode;
        _theme.ApplyColorVision(mode);

        if (changed)
            Changed?.Invoke();
    }

    /// <summary>Turns banner announcements on or off. Nothing to apply — the shell reads this.</summary>
    internal void SetAnnounceUpdates(bool enabled) {
        if (enabled == AnnounceUpdates)
            return;

        AnnounceUpdates = enabled;
        Changed?.Invoke();
    }
}
