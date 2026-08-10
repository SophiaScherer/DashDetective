using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// The rules every platform's built-in table has to hold, whichever platform authored it. One subclass
/// per catalog runs the whole set, so a rule added here is asked of all of them and a table that
/// breaks one names itself in the failure.
///
/// Deliberately not parameterised on the host: a catalog is string literals, so <b>every one of these
/// runs on a Windows dev machine and on the Linux CI leg alike</b>. That matters — a test that branched
/// on <c>OperatingSystem.IsLinux()</c> would leave the other table's rules unchecked wherever it ran.
///
/// Rules that are genuinely one platform's — the exact set of rows allowed to elevate, and the wording
/// they use to say so — stay in that catalog's own test class.
/// </summary>
public abstract class ToolkitCatalogInvariants {
    /// <summary>The table under test.</summary>
    protected abstract IReadOnlyList<ToolkitEntry> Entries { get; }

    /// <summary>The catalog under test, for the rules that run through the page.</summary>
    private protected abstract IToolkitCatalog Catalog { get; }

    /// <summary>Every row must say what it is and what it does — a blank description leaves the row
    /// half-drawn and, because the search provider matches on it, unreachable by intent.</summary>
    [Fact]
    public void Entries_AllCarryACommandAndADescription() {
        Assert.NotEmpty(Entries);
        Assert.All(Entries, entry => {
            Assert.False(string.IsNullOrWhiteSpace(entry.Command));
            Assert.False(string.IsNullOrWhiteSpace(entry.Description));
        });
    }

    /// <summary>The command text is a row's identity: universal search reveals by it, and
    /// <c>ToolkitView.FindRow</c> matches the first row whose Tag equals it. A duplicate would make one
    /// of the pair permanently unreachable.</summary>
    [Fact]
    public void Entries_HaveDistinctCommands() {
        var commands = Entries.Select(entry => entry.Command).ToList();

        Assert.Equal(commands.Count, commands.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Entries_AllCarryAnActionWithATarget() {
        Assert.All(Entries,
                   entry => Assert.False(string.IsNullOrWhiteSpace(entry.Action.Target)));
    }

    /// <summary>Every category the entries claim must be one the list actually renders, or the rows
    /// filed under it would be dropped by <see cref="ToolkitFilter.Group"/> and never appear.</summary>
    [Fact]
    public void Entries_OnlyUseCategoriesTheListRenders() {
        Assert.All(Entries,
                   entry => Assert.Contains(entry.Category, ToolkitCatalog.Categories));
    }

    /// <summary>Every section the tab draws has something in it. A table missing one is a platform
    /// half-ported rather than a deliberate choice, and the empty chip would read as a bug.</summary>
    [Fact]
    public void Entries_FillEverySectionExceptTheUsersOwn() {
        var filled = Entries.Select(entry => entry.Category).Distinct().ToList();

        Assert.Equal(ToolkitCatalog.Categories.Where(c => c != ToolkitCategory.Custom).ToList(),
                     ToolkitCatalog.Categories.Where(filled.Contains).ToList());
    }

    /// <summary>A catalog row is nobody's custom row — the flag has to be driven by having a source,
    /// not by a category that could be set by hand.</summary>
    [Fact]
    public void Entries_AreNeverMarkedAsTheUsersOwn() {
        Assert.All(Entries, entry => {
            Assert.False(entry.IsCustom);
            Assert.Null(entry.SecondaryCategory);
        });
    }

    /// <summary>Elevation is opt-in per entry and raises a consent prompt, so an accidental one is worth
    /// catching. Opening a folder, a link or a tool window never needs it — only a console command
    /// can legitimately ask.</summary>
    [Fact]
    public void Entries_OnlyConsoleCommandsEverRequireElevation() {
        Assert.All(Entries.Where(e => e.Action.RequiresElevation),
                   entry => Assert.Equal(ToolkitEntryKind.Command, entry.Kind));
    }

    /// <summary>An elevated row must never claim to capture: Windows refuses to redirect a <c>runas</c>
    /// process's streams, so a captured-and-elevated row would show an empty body forever.</summary>
    [Fact]
    public void Entries_ElevatedRowsDoNotCaptureOutput() {
        Assert.All(Entries.Where(e => e.RequiresElevation),
                   entry => Assert.False(entry.Action.CapturesOutput));
    }

    /// <summary>A row whose label carries a placeholder must have a box to fill it from, and a row with
    /// a box must say so in its label. Either half alone is a row the user cannot use correctly.</summary>
    [Fact]
    public void Entries_PlaceholderInTheLabelMatchesHavingAParameter() {
        Assert.All(Entries, entry => Assert.Equal(
            entry.Command.Contains('<', StringComparison.Ordinal),
            entry.Parameter is not null));
    }

    /// <summary>The typed value is appended as the last argument, so a parameterised row has to be one
    /// whose command takes its target last — which in practice means a captured console command.</summary>
    [Fact]
    public void Entries_OnlyCapturedCommandsTakeAParameter() {
        Assert.All(Entries.Where(e => e.Parameter is not null),
                   entry => Assert.True(entry.Action.CapturesOutput));
    }

    /// <summary>The runner refuses anything that is not https, so a catalog link that is not https is a
    /// row that can only ever fail. Catching it here means it never ships as a dead button.</summary>
    [Fact]
    public void Entries_EveryDocumentationLinkIsHttps() {
        var links = Entries.Where(e => e.Action.Kind == ToolkitActionKind.OpenUrl).ToList();

        Assert.NotEmpty(links);
        Assert.All(links, entry => Assert.StartsWith(
            "https://", entry.Action.Target, StringComparison.Ordinal));
    }

    /// <summary>A link row is labelled by title, so the URL would otherwise be invisible on the page.
    /// It reaches the log through the action's command line, which is what puts it on the record.</summary>
    [Fact]
    public void Entries_DocumentationLinksCarryTheUrlOnTheirCommandLine() {
        Assert.All(Entries.Where(e => e.Action.Kind == ToolkitActionKind.OpenUrl),
                   entry => Assert.Equal(entry.Action.Target, entry.Action.CommandLine));
    }

    /// <summary>Nothing outside Docs &amp; Links opens a browser, and nothing inside it does anything
    /// else — a mis-filed link would send the user to the web from a row that reads as a local tool.</summary>
    [Fact]
    public void Entries_OpenUrlIsUsedOnlyByDocumentationLinks() {
        Assert.All(Entries, entry => Assert.Equal(
            entry.Action.Kind == ToolkitActionKind.OpenUrl,
            entry.Category == ToolkitCategory.DocsAndLinks));
    }

    // ----- Through the page -----

    /// <summary>Author, save, reload, run: a user's command survives a round trip through the codec and
    /// arrives as a runnable row beside this platform's own. Nothing about that is platform-specific,
    /// which is exactly why it is worth asking of every catalog.</summary>
    [Fact]
    public void CustomCommand_SurvivesEncodeAndReloadBesideTheBuiltInRows() {
        var page = new ToolkitViewModel(Catalog);
        page.AddCommand(new ToolkitCommand(
            "zzz-my-own", "Something only I have", ToolkitCommandType.Capture, "thing", "-a -b"));

        var restored = new ToolkitViewModel(Catalog);
        restored.LoadCommands(page.EncodeCommands());

        var mine = Assert.Single(restored.Custom);
        Assert.Equal("zzz-my-own", mine.Command);
        Assert.Equal(["-a", "-b"], mine.Action.Arguments);
        Assert.Equal(Entries.Count + 1, restored.AllEntries.Count);
    }

    /// <summary>A pin naming one of the user's rows still finds it after a reload — the reason commands
    /// are applied before pins, and the reason pins are stored by text rather than by index.</summary>
    [Fact]
    public void CustomCommand_KeepsItsPinAcrossAReload() {
        var page = new ToolkitViewModel(Catalog);
        page.AddCommand(new ToolkitCommand(
            "zzz-my-own", "Something only I have", ToolkitCommandType.Launch, "thing"));
        page.Custom[0].IsPinned = true;

        var restored = new ToolkitViewModel(Catalog);
        restored.LoadCommands(page.EncodeCommands());
        restored.LoadPins(page.EncodePins());

        Assert.True(Assert.Single(restored.Custom).IsPinned);
        restored.LoadPins("");
    }

    /// <summary>The form checks a new title against whatever is live on the page, so a user cannot
    /// shadow one of this platform's built-in rows — pins and search reveal are keyed by command text,
    /// and two rows sharing one would make both ambiguous.</summary>
    [Fact]
    public void CustomCommand_CannotTakeTheTitleOfABuiltInRow() {
        var page = new ToolkitViewModel(Catalog);
        var taken = Entries[0].Command;

        var refusal = ToolkitCommandValidator.Validate(
            new ToolkitCommand(taken, "", ToolkitCommandType.Launch, "thing"), page.AllEntries);

        Assert.Equal(ToolkitCommandValidator.TitleTaken, refusal);
    }

    /// <summary>Nothing the form can produce elevates, whichever catalog it is authored beside:
    /// <see cref="ToolkitCommandType"/> has no elevated member, which is what makes that structural
    /// rather than a rule someone has to remember.</summary>
    [Fact]
    public void CustomCommand_CanNeverBeElevated() {
        var page = new ToolkitViewModel(Catalog);
        foreach (var type in Enum.GetValues<ToolkitCommandType>())
            page.AddCommand(new ToolkitCommand($"zzz-{type}", "", type, "https://example.com"));

        Assert.All(page.Custom, entry => Assert.False(entry.RequiresElevation));
    }
}

/// <summary>The Windows table, run against every shared rule.</summary>
public class WindowsToolkitCatalogInvariants : ToolkitCatalogInvariants {
    protected override IReadOnlyList<ToolkitEntry> Entries => WindowsToolkitCatalog.Instance.Entries;

    private protected override IToolkitCatalog Catalog => WindowsToolkitCatalog.Instance;
}

/// <summary>The Linux table, run against every shared rule.</summary>
public class LinuxToolkitCatalogInvariants : ToolkitCatalogInvariants {
    protected override IReadOnlyList<ToolkitEntry> Entries => LinuxToolkitCatalog.Instance.Entries;

    private protected override IToolkitCatalog Catalog => LinuxToolkitCatalog.Instance;
}
