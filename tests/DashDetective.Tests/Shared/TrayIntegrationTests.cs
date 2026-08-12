using DashDetective.Shared;
using DashDetective.Tabs.Settings;
using System;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>
/// Covers <see cref="TrayIntegration"/> and the copy that depends on it. The rule it encodes is a
/// safety one: the tray setting is <b>on by default</b>, so a desktop that shows no tray icon would
/// otherwise hide the window on close with no way to bring it back.
/// </summary>
public class TrayIntegrationTests {
    /// <summary>Windows has a notification area; the desktops this port targets are not guaranteed to,
    /// and there is no reliable way to ask at startup.</summary>
    [Fact]
    public void HidesOnClose_OnlyWhereThereIsATrayToHideInto() =>
        Assert.Equal(OperatingSystem.IsWindows(), TrayIntegration.HidesOnClose);

    /// <summary>Both arms asserted explicitly, so neither depends on which host runs the suite. The
    /// unavailable wording must describe what closing actually does — a disabled toggle explaining a
    /// behaviour it cannot produce is worse than no explanation.</summary>
    [Fact]
    public void ShowInTrayFor_DescribesWhatClosingActuallyDoes() {
        Assert.Equal("Keep console running in background",
                     SettingDescriptions.ShowInTrayFor(hidesOnClose: true));
        Assert.Contains("closing exits the app",
                        SettingDescriptions.ShowInTrayFor(hidesOnClose: false),
                        StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShowInTray_ResolvesForThisHost() =>
        Assert.Equal(SettingDescriptions.ShowInTrayFor(TrayIntegration.HidesOnClose),
                     SettingDescriptions.ShowInTray);

    /// <summary>The page's label and the text universal search matches are the same string, so the
    /// catalog must take it from the seam rather than keep a copy that could drift.</summary>
    [Fact]
    public void Catalog_TakesItsTrayDescriptionFromTheSeam() =>
        Assert.Equal(SettingDescriptions.ShowInTray, SettingCatalog.Instance.ShowInTray.Description);

    /// <summary>The setting stays in the catalog wherever it runs — it is disabled, not removed — so
    /// the search index does not vary by platform and a preference carried between machines survives.</summary>
    [Fact]
    public void Catalog_StillOffersTheTraySettingEverywhere() =>
        Assert.Contains(SettingCatalog.Instance.All, e => e.Id == SettingId.ShowInTray);
}
