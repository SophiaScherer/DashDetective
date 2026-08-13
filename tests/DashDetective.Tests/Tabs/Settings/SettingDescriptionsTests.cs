using DashDetective.Tabs.Settings;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Covers <see cref="SettingDescriptions"/>: both arms of the copy that cannot be shared, and
/// that the catalog actually reads it rather than holding a literal of its own.</summary>
public class SettingDescriptionsTests {
    /// <summary>Both arms are asserted explicitly, so neither depends on which host runs the suite —
    /// the whole reason the platform is a parameter.</summary>
    [Fact]
    public void LaunchAtStartupFor_NamesEachPlatformsOwnMechanism() {
        Assert.Equal("Start with Windows", SettingDescriptions.LaunchAtStartupFor(windows: true));
        Assert.Equal("Start when you log in", SettingDescriptions.LaunchAtStartupFor(windows: false));
    }

    /// <summary>The resolved value has to match the host, or the page and search would show the other
    /// platform's wording.</summary>
    [Fact]
    public void LaunchAtStartup_ResolvesForThisHost() =>
        Assert.Equal(SettingDescriptions.LaunchAtStartupFor(OperatingSystem.IsWindows()),
                     SettingDescriptions.LaunchAtStartup);

    /// <summary>The page's label and the text universal search matches are the same string, so the
    /// catalog must take it from here rather than keep a copy that could drift.</summary>
    [Fact]
    public void Catalog_TakesItsStartupDescriptionFromTheSeam() =>
        Assert.Equal(SettingDescriptions.LaunchAtStartup,
                     SettingCatalog.Instance.LaunchAtStartup.Description);

    /// <summary>"Windows" must not survive into the copy on a machine that has none — the bug this seam
    /// exists to prevent.</summary>
    [Fact]
    public void Catalog_DoesNotSayWindowsOffWindows() {
        if (OperatingSystem.IsWindows())
            return;

        Assert.DoesNotContain("Windows", SettingCatalog.Instance.LaunchAtStartup.Description,
                              StringComparison.OrdinalIgnoreCase);
    }
}
