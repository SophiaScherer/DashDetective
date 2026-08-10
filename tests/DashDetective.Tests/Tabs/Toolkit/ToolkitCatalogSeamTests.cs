using DashDetective.Tabs.Toolkit;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="IToolkitCatalog.ForCurrentPlatform"/> and the empty third arm.
///
/// This is the only place in the Toolkit tests that branches on the host — every other assertion names
/// the catalog it is about, so a Windows run still exercises it. Keep it that way: a branching test is
/// invisible on the platform it does not run on.
/// </summary>
public class ToolkitCatalogSeamTests {
    [Fact]
    public void ForCurrentPlatform_ResolvesTheCatalogForThisMachine() {
        var catalog = IToolkitCatalog.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsToolkitCatalog>(catalog);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxToolkitCatalog>(catalog);
        else
            Assert.IsType<UnsupportedToolkitCatalog>(catalog);
    }

    /// <summary>The rows carry live pin state, so the catalog has to be the same object every time —
    /// a fresh list per call would silently give each reader its own unpinned copy.</summary>
    [Fact]
    public void ForCurrentPlatform_ReturnsTheSameCatalogEachTime() {
        Assert.Same(IToolkitCatalog.ForCurrentPlatform(), IToolkitCatalog.ForCurrentPlatform());
    }

    /// <summary>A platform with no table offers nothing rather than another platform's rows — the page
    /// falls back to its own empty state and the user can still author their own.</summary>
    [Fact]
    public void Unsupported_OffersNoRowsAtAll() {
        Assert.Empty(UnsupportedToolkitCatalog.Instance.Entries);
    }

    /// <summary>The page over an empty catalog must still build, and must say it has nothing rather
    /// than looking half-drawn.</summary>
    [Fact]
    public void Page_OverAnEmptyCatalog_ShowsTheEmptyState() {
        var page = new ToolkitViewModel(UnsupportedToolkitCatalog.Instance);

        Assert.Empty(page.AllEntries);
        Assert.False(page.HasCommands);
        Assert.Empty(page.Groups);
    }

    /// <summary>An empty built-in table is not an unusable page: the user's own commands still land on
    /// it, which is the whole reason the third arm is empty rather than another platform's rows.</summary>
    [Fact]
    public void Page_OverAnEmptyCatalog_StillTakesTheUsersOwnCommands() {
        var page = new ToolkitViewModel(UnsupportedToolkitCatalog.Instance);

        page.AddCommand(new ToolkitCommand(
            "zzz-my-own", "Something only I have", ToolkitCommandType.Launch, "thing.exe"));

        Assert.True(page.HasCommands);
        Assert.Equal("zzz-my-own", Assert.Single(page.AllEntries).Command);
    }
}
