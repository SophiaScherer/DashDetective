using DashDetective.Services.Accessibility;
using DashDetective.Services.Settings;
using DashDetective.Services.Theming;
using Xunit;

namespace DashDetective.Tests.Services.Accessibility;

/// <summary>
/// Covers <see cref="AccessibilityService"/>: it is the single owner of the Accessibility card's state,
/// so a selection, a restore and a persisted value all have to move it the same way, and only a real
/// change may be announced.
/// </summary>
public class AccessibilityServiceTests {
    private static AccessibilityService Create() => new(new ThemeService());

    [Fact]
    public void Ctor_StartsAtTheShippedScale() =>
        Assert.Equal(UiScale.DefaultPercent, Create().ScalePercent);

    [Fact]
    public void SetScalePercent_SelectsTheStepAndItsFactor() {
        var service = Create();

        service.SetScalePercent(150);

        Assert.Equal(150, service.ScalePercent);
        Assert.Equal(1.5, service.ScaleFactor);
    }

    /// <summary>The card's segmented control reads <see cref="AccessibilityService.ScalePercent"/> back,
    /// so an unrecognized stored value has to arrive on the ladder or no segment shows as selected.</summary>
    [Fact]
    public void Apply_UnknownPercent_LandsOnTheLadder() {
        var service = Create();

        service.Apply(AppSettings.Defaults with { UiScalePercent = 133 });

        Assert.Contains(service.ScalePercent, UiScale.Percents);
    }

    [Fact]
    public void Changed_RaisedOnceForARealChange() {
        var service = Create();
        var raised = 0;
        service.Changed += () => raised++;

        service.SetScalePercent(200);

        Assert.Equal(1, raised);
    }

    /// <summary>Startup pushes the value through whether or not it differs, so re-selecting what is
    /// already chosen must stay silent — otherwise every launch would report a change and persist.</summary>
    [Fact]
    public void Changed_NotRaisedWhenTheValueIsUnchanged() {
        var service = Create();
        service.SetScalePercent(125);

        var raised = 0;
        service.Changed += () => raised++;
        service.SetScalePercent(125);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void RestoreDefaults_PutsTheScaleBackAndAnnouncesIt() {
        var service = Create();
        service.SetScalePercent(175);

        var raised = 0;
        service.Changed += () => raised++;
        service.RestoreDefaults();

        Assert.Equal(UiScale.DefaultPercent, service.ScalePercent);
        Assert.Equal(1, raised);
    }
    /// <summary>Both visible-change options ship off, so switching nothing leaves the app looking exactly
    /// as it did before the accessibility work.</summary>
    [Fact]
    public void Ctor_ShipsWithTheVisibleChangesOff() {
        var service = Create();

        Assert.False(service.HighContrast);
        Assert.False(service.DistinguishWithoutColor);
    }

    [Fact]
    public void SetHighContrast_TogglesAndAnnouncesOnlyRealChanges() {
        var service = Create();
        var raised = 0;
        service.Changed += () => raised++;

        service.SetHighContrast(true);
        service.SetHighContrast(true);

        Assert.True(service.HighContrast);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetDistinguishWithoutColor_TogglesAndAnnouncesOnlyRealChanges() {
        var service = Create();
        var raised = 0;
        service.Changed += () => raised++;

        service.SetDistinguishWithoutColor(true);
        service.SetDistinguishWithoutColor(true);

        Assert.True(service.DistinguishWithoutColor);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Apply_SeedsEveryOptionFromSettings() {
        var service = Create();

        service.Apply(AppSettings.Defaults with {
            UiScalePercent = 150,
            HighContrast = true,
            DistinguishWithoutColor = true,
            KeyboardReordering = false,
            TextScalePercent = 125,
        });

        Assert.Equal(150, service.ScalePercent);
        Assert.Equal(125, service.TextScalePercent);
        Assert.True(service.HighContrast);
        Assert.True(service.DistinguishWithoutColor);
        Assert.False(service.KeyboardReordering);
    }

    /// <summary>"Restore defaults" resets the WHOLE card, which is what lets a later option be covered by
    /// it for free simply by becoming a property on this service.</summary>
    [Fact]
    public void RestoreDefaults_ResetsEveryOption() {
        var service = Create();
        service.SetScalePercent(200);
        service.SetHighContrast(true);
        service.SetDistinguishWithoutColor(true);
        service.SetKeyboardReordering(false);
        service.SetTextScalePercent(200);

        service.RestoreDefaults();

        Assert.Equal(UiScale.DefaultPercent, service.ScalePercent);
        Assert.Equal(TextScale.DefaultPercent, service.TextScalePercent);
        Assert.False(service.HighContrast);
        Assert.False(service.DistinguishWithoutColor);

        // The one option that defaults ON, so a reset that only cleared flags would miss it.
        Assert.True(service.KeyboardReordering);
    }
}
