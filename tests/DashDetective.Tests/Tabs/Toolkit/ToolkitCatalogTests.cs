using DashDetective.Tabs.Toolkit;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitCatalog"/>: the copy table stays complete (every category and
/// every kind reads as something, and no two share a label) and the display order matches the enum's
/// declaration order, so adding a category can't silently fall through to another one's text.</summary>
public class ToolkitCatalogTests {
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

    /// <summary>Every row must say what it is and what it does — a blank description leaves the row
    /// half-drawn and, because the search provider matches on it, unreachable by intent.</summary>
    [Fact]
    public void Entries_AllCarryACommandAndADescription() {
        Assert.NotEmpty(ToolkitCatalog.Entries);
        Assert.All(ToolkitCatalog.Entries, entry => {
            Assert.False(string.IsNullOrWhiteSpace(entry.Command));
            Assert.False(string.IsNullOrWhiteSpace(entry.Description));
        });
    }

    /// <summary>The command text is a row's identity: universal search reveals by it, and
    /// <c>ToolkitView.FindRow</c> matches the first row whose Tag equals it. A duplicate would make one
    /// of the pair permanently unreachable.</summary>
    [Fact]
    public void Entries_HaveDistinctCommands() {
        var commands = ToolkitCatalog.Entries.Select(entry => entry.Command).ToList();

        Assert.Equal(commands.Count, commands.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Entries_AllCarryAnActionWithATarget() {
        Assert.All(ToolkitCatalog.Entries,
                   entry => Assert.False(string.IsNullOrWhiteSpace(entry.Action.Target)));
    }

    /// <summary>Every category the entries claim must be one the list actually renders, or the rows
    /// filed under it would be dropped by <see cref="ToolkitFilter.Group"/> and never appear.</summary>
    [Fact]
    public void Entries_OnlyUseCategoriesTheListRenders() {
        Assert.All(ToolkitCatalog.Entries,
                   entry => Assert.Contains(entry.Category, ToolkitCatalog.Categories));
    }

    /// <summary>Elevation is opt-in per entry and raises a UAC prompt, so an accidental one is worth
    /// catching. Opening a folder, a link or a tool window never needs admin — only a console command
    /// can legitimately ask for it.</summary>
    [Fact]
    public void Entries_OnlyConsoleCommandsEverRequireElevation() {
        Assert.All(ToolkitCatalog.Entries.Where(e => e.Action.RequiresElevation),
                   entry => Assert.Equal(ToolkitEntryKind.Command, entry.Kind));
    }

    /// <summary>A row whose label carries a placeholder must have a box to fill it from, and a row with
    /// a box must say so in its label. Either half alone is a row the user cannot use correctly.</summary>
    [Fact]
    public void Entries_PlaceholderInTheLabelMatchesHavingAParameter() {
        Assert.All(ToolkitCatalog.Entries, entry => Assert.Equal(
            entry.Command.Contains('<', StringComparison.Ordinal),
            entry.Parameter is not null));
    }

    /// <summary>The typed value is appended as the last argument, so a parameterised row has to be one
    /// whose command takes its target last — which in practice means a captured console command.</summary>
    [Fact]
    public void Entries_OnlyCapturedCommandsTakeAParameter() {
        Assert.All(ToolkitCatalog.Entries.Where(e => e.Parameter is not null),
                   entry => Assert.True(entry.Action.CapturesOutput));
    }

    /// <summary>Elevation is the app's one privileged act, so the set of rows that ask for it is pinned
    /// by name: adding another must be a deliberate edit here, not something that slips in.</summary>
    [Fact]
    public void Entries_ExactlyOneRowAsksForAdministrator() {
        var elevated = ToolkitCatalog.Entries.Where(e => e.RequiresElevation).ToList();

        Assert.Equal(["sfc /scannow"], elevated.Select(e => e.Command));
    }

    /// <summary>An elevated row must never claim to capture: Windows refuses to redirect a <c>runas</c>
    /// process's streams, so a captured-and-elevated row would show an empty body forever.</summary>
    [Fact]
    public void Entries_ElevatedRowsDoNotCaptureOutput() {
        Assert.All(ToolkitCatalog.Entries.Where(e => e.RequiresElevation),
                   entry => Assert.False(entry.Action.CapturesOutput));
    }

    /// <summary>A row that raises a UAC prompt has to say so in its own text — the shield is a marker,
    /// not the explanation, and it is invisible to anyone reading the row through search.</summary>
    [Fact]
    public void Entries_ElevatedRowsSaySoInTheirDescription() {
        Assert.All(ToolkitCatalog.Entries.Where(e => e.RequiresElevation),
                   entry => Assert.Contains("administrator", entry.Description,
                                            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Nothing outside Docs &amp; Links opens a browser, and nothing inside it does anything
    /// else — a mis-filed link would send the user to the web from a row that reads as a local tool.</summary>
    [Fact]
    public void Entries_OpenUrlIsUsedOnlyByDocumentationLinks() {
        Assert.All(ToolkitCatalog.Entries, entry => Assert.Equal(
            entry.Action.Kind == ToolkitActionKind.OpenUrl,
            entry.Category == ToolkitCategory.DocsAndLinks));
    }
}
