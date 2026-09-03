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
}
