using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitCatalog"/>'s copy (every category and every kind reads as
/// something, no two share a label, and the display order matches the enum's declaration order, so
/// adding a category can't silently fall through to another one's text) and what is particular to the
/// Windows command table. The rules it shares with every other catalog are in
/// <see cref="ToolkitCatalogInvariants"/>.
///
/// The table is named rather than resolved through <see cref="IToolkitCatalog.ForCurrentPlatform"/>:
/// these rules are about <see cref="WindowsToolkitCatalog"/>'s own rows, and asserting them against
/// whatever the host happens to return would stop them running on the Linux CI leg.</summary>
public class ToolkitCatalogTests {
    private static IReadOnlyList<ToolkitEntry> Entries => WindowsToolkitCatalog.Instance.Entries;

    [Fact]
    public void Categories_ListEveryValueInDeclarationOrder() {
        Assert.Equal(Enum.GetValues<ToolkitCategory>(), ToolkitCatalog.Categories);
    }

    [Fact]
    public void HeaderFor_NamesEveryCategoryDistinctly() {
        var headers = Enum.GetValues<ToolkitCategory>().Select(ToolkitCatalog.HeaderFor).ToList();

        Assert.All(headers, h => Assert.False(string.IsNullOrWhiteSpace(h)));
        Assert.Equal(headers.Count, headers.Distinct().Count());
    }

    [Fact]
    public void LabelFor_NamesEveryKindDistinctly() {
        var labels = Enum.GetValues<ToolkitEntryKind>().Select(ToolkitCatalog.LabelFor).ToList();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    /// <summary>Elevation is the app's one privileged act, so the set of rows that ask for it is pinned
    /// by name: adding another must be a deliberate edit here, not something that slips in.</summary>
    [Fact]
    public void Entries_ExactlyOneRowAsksForAdministrator() {
        var elevated = Entries.Where(e => e.RequiresElevation).ToList();

        Assert.Equal(["sfc /scannow"], elevated.Select(e => e.Command));
    }

    /// <summary>A row that raises a UAC prompt has to say so in its own text — the shield is a marker,
    /// not the explanation, and it is invisible to anyone reading the row through search. The wording is
    /// Windows', which is why this rule is not one of the shared ones.</summary>
    [Fact]
    public void Entries_ElevatedRowsSaySoInTheirDescription() {
        Assert.All(Entries.Where(e => e.RequiresElevation),
                   entry => Assert.Contains("administrator", entry.Description,
                                            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Environment variables are left unexpanded in the table so the runner resolves them per
    /// session; a row that baked in one session's value would go stale for every other.</summary>
    [Fact]
    public void Folders_AreWrittenInTheNotationTheUserWouldType() {
        Assert.All(Entries.Where(e => e.Category == ToolkitCategory.Folders),
                   entry => Assert.True(entry.Action.Target.StartsWith('%') ||
                                        entry.Action.Target.StartsWith("shell:", StringComparison.Ordinal)));
    }

    /// <summary>Every folder row reaches the app's own File Explorer except the one shell location,
    /// which resolves through the shell namespace and has no path to navigate to — that row offers the
    /// external icon alone.
    ///
    /// Windows-only, unlike its Linux counterpart, and the asymmetry is real rather than an oversight:
    /// <see cref="ToolkitPaths"/>' platform seam switches which notation is expanded, but <c>%appdata%</c>
    /// still needs the variable to exist, and it does not on a Linux runner. The Unix notation has no
    /// such dependency, so <c>LinuxToolkitCatalogTests</c> checks its rows from either host.</summary>
    [Fact]
    public void Folders_AllReachTheAppsOwnExplorerExceptTheShellLocation() {
        if (!OperatingSystem.IsWindows())
            return;

        var unreachable = Entries
            .Where(e => e.IsPathEntry && !ToolkitPaths.IsFileSystemPath(e.Action.Target, windows: true))
            .Select(e => e.Command)
            .ToList();

        Assert.Equal(["shell:startup"], unreachable);
    }
}
