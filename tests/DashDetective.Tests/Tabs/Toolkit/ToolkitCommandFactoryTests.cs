using DashDetective.Tabs.Toolkit;
using DashDetective.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="ToolkitCommandFactory"/>: a user's command becomes an ordinary
/// <see cref="ToolkitEntry"/> down the same paths the catalog uses. The safety-shaped assertions here are
/// that arguments stay a list and that nothing the factory can produce is elevated.
/// </summary>
public class ToolkitCommandFactoryTests {
    // A rooted folder path: ToEntry's CanOpenInApp is decided by Path.IsPathRooted, so a drive-letter
    // literal would read as an unrooted name off Windows and the row would offer one icon, not two.
    private static readonly string Folder = TestPaths.Of("work");

    [Theory]
    [InlineData(ToolkitCommandType.FolderPath, ToolkitActionKind.OpenPath)]
    [InlineData(ToolkitCommandType.Url, ToolkitActionKind.OpenUrl)]
    [InlineData(ToolkitCommandType.Launch, ToolkitActionKind.Launch)]
    [InlineData(ToolkitCommandType.Capture, ToolkitActionKind.Capture)]
    public void ActionFor_MapsEachTypeToItsRunPath(
        ToolkitCommandType type, ToolkitActionKind expected) {
        var action = ToolkitCommandFactory.ActionFor(
            new ToolkitCommand("t", "d", type, "payload"));

        Assert.Equal(expected, action.Kind);
    }

    /// <summary>The whole point of the type having no elevated member: there is no combination of form
    /// input that produces a row which can raise a UAC prompt.</summary>
    [Fact]
    public void ActionFor_NoTypeCanEverProduceAnElevatedAction() {
        var types = Enum.GetValues<ToolkitCommandType>();

        Assert.All(types, type => Assert.False(
            ToolkitCommandFactory
                .ActionFor(new ToolkitCommand("t", "d", type, "payload", "/f"))
                .RequiresElevation));
    }

    /// <summary>Arguments reach the action already split, so they can only ever be handed to the OS as
    /// separate list elements — never concatenated into a command line.</summary>
    [Fact]
    public void ActionFor_SplitsTheArgumentsIntoSeparateElements() {
        var action = ToolkitCommandFactory.ActionFor(
            new ToolkitCommand("Ports", "", ToolkitCommandType.Capture, "netstat", "-an -p tcp"));

        Assert.Equal("netstat", action.Target);
        Assert.Equal(["-an", "-p", "tcp"], action.Arguments);
    }

    /// <summary>A metacharacter stays one ordinary argument: there is no shell between here and the
    /// process, so it cannot start a second command.</summary>
    [Fact]
    public void ActionFor_ShellMetacharactersStayOrdinaryArguments() {
        var action = ToolkitCommandFactory.ActionFor(
            new ToolkitCommand("Ports", "", ToolkitCommandType.Capture, "netstat", "-an & calc"));

        Assert.Equal(["-an", "&", "calc"], action.Arguments);
        Assert.Equal("netstat", action.Target);
    }

    [Fact]
    public void ActionFor_FolderAndUrlTakeNoArguments() {
        var folder = ToolkitCommandFactory.ActionFor(
            new ToolkitCommand("f", "", ToolkitCommandType.FolderPath, Folder, "ignored"));
        var url = ToolkitCommandFactory.ActionFor(
            new ToolkitCommand("u", "", ToolkitCommandType.Url, "https://example.com", "ignored"));

        Assert.Empty(folder.Arguments);
        Assert.Empty(url.Arguments);
    }

    [Fact]
    public void ToEntry_CarriesTheTypedTextOntoTheRow() {
        var command = new ToolkitCommand(
            "My folder", "Somewhere I go often", ToolkitCommandType.FolderPath, Folder);

        var entry = ToolkitCommandFactory.ToEntry(command);

        Assert.Equal("My folder", entry.Command);
        Assert.Equal("Somewhere I go often", entry.Description);
        Assert.Same(command, entry.Source);
        Assert.True(entry.IsCustom);
    }

    /// <summary>A custom row is filed under Custom; the category the user picked rides along separately,
    /// so the filter can place it a second time without the row forgetting whose it is.</summary>
    [Fact]
    public void ToEntry_FilesUnderCustomAndRemembersThePickedCategory() {
        var entry = ToolkitCommandFactory.ToEntry(new ToolkitCommand(
            "Ports", "", ToolkitCommandType.Capture, "netstat", "-an", ToolkitCategory.Diagnostics));

        Assert.Equal(ToolkitCategory.Custom, entry.Category);
        Assert.Equal(ToolkitCategory.Diagnostics, entry.SecondaryCategory);
    }

    [Fact]
    public void ToEntry_NoCategoryPicked_HasNoSecondaryOne() {
        Assert.Null(ToolkitCommandFactory.ToEntry(
            new ToolkitCommand("Mine", "", ToolkitCommandType.Launch, "thing.exe")).SecondaryCategory);
    }

    /// <summary>A folder a user typed gets the same pair of open icons the authored folder rows have.</summary>
    [Fact]
    public void ToEntry_FolderCommand_OffersBothExplorers() {
        var entry = ToolkitCommandFactory.ToEntry(
            new ToolkitCommand("Mine", "", ToolkitCommandType.FolderPath, Folder));

        Assert.True(entry.IsPathEntry);
        Assert.True(entry.CanOpenInApp);
    }

    [Theory]
    [InlineData(ToolkitCommandType.FolderPath, ToolkitEntryKind.Folder)]
    [InlineData(ToolkitCommandType.Url, ToolkitEntryKind.Link)]
    [InlineData(ToolkitCommandType.Launch, ToolkitEntryKind.App)]
    [InlineData(ToolkitCommandType.Capture, ToolkitEntryKind.Command)]
    public void KindFor_ReusesTheExistingBadges(
        ToolkitCommandType type, ToolkitEntryKind expected) {
        Assert.Equal(expected, ToolkitCommandFactory.KindFor(type));
    }

    [Fact]
    public void LabelFor_NamesEveryTypeForThePicker() {
        Assert.All(Enum.GetValues<ToolkitCommandType>(),
                   type => Assert.False(string.IsNullOrWhiteSpace(
                       ToolkitCommandFactory.LabelFor(type))));

        Assert.Equal(Enum.GetValues<ToolkitCommandType>().Length,
                     Enum.GetValues<ToolkitCommandType>()
                         .Select(ToolkitCommandFactory.LabelFor).Distinct().Count());
    }
}
