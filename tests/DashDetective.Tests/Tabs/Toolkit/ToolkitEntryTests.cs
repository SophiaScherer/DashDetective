using DashDetective.Tabs.Toolkit;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Pins what <see cref="ToolkitEntry"/> says about its own default action: every row names what
/// activating it does before it is activated, worded per action kind, and that wording leads the row's
/// tooltip and its screen-reader name.
/// </summary>
public class ToolkitEntryTests {
    private static readonly string Folder = OperatingSystem.IsWindows() ? "%windir%" : "~";

    private static ToolkitEntry Row(ToolkitAction action, string command = "cmd", string description = "What it does") =>
        new(command, description, ToolkitCategory.SystemTools, ToolkitEntryKind.App, action);

    [Theory]
    [InlineData(ToolkitActionKind.OpenUrl, "Open in your browser")]
    [InlineData(ToolkitActionKind.Launch, "Launch in its own window")]
    [InlineData(ToolkitActionKind.Capture, "Run — output goes to the Execution Log")]
    [InlineData(ToolkitActionKind.Elevated, "Run as administrator")]
    public void DefaultActionFor_EachRunnableKind_SaysWhatActivatingDoes(ToolkitActionKind kind, string expected) {
        Assert.Equal(expected, ToolkitEntry.DefaultActionFor(kind, canOpenInApp: false, windows: true));
        Assert.Equal(expected, ToolkitEntry.DefaultActionFor(kind, canOpenInApp: false, windows: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DefaultActionFor_FolderTheAppCanOpen_NamesTheInAppFileExplorer(bool windows) =>
        Assert.Equal("Open in DashDetective's File Explorer",
                     ToolkitEntry.DefaultActionFor(ToolkitActionKind.OpenPath, canOpenInApp: true, windows));

    /// <summary>Linux has no Windows Explorer, so a location the app cannot open names the platform's
    /// file manager generically rather than borrowing the Windows name.</summary>
    [Theory]
    [InlineData(true, "Open in Windows Explorer")]
    [InlineData(false, "Open in your file manager")]
    public void DefaultActionFor_FolderTheAppCannotOpen_NamesThePlatformsFileManager(bool windows, string expected) =>
        Assert.Equal(expected, ToolkitEntry.DefaultActionFor(ToolkitActionKind.OpenPath, canOpenInApp: false, windows));

    [Fact]
    public void DefaultActionText_RootedFolderRow_OpensInTheApp() =>
        Assert.Equal("Open in DashDetective's File Explorer", Row(ToolkitAction.OpenPath(Folder)).DefaultActionText);

    /// <summary>A shell: row has no path the app could navigate to, so its default is the shell.</summary>
    [Fact]
    public void DefaultActionText_ShellLocationRow_GoesToTheShell() =>
        Assert.Equal(ToolkitEntry.DefaultActionFor(ToolkitActionKind.OpenPath, false, OperatingSystem.IsWindows()),
                     Row(ToolkitAction.OpenPath("shell:startup")).DefaultActionText);

    [Fact]
    public void ActivationTip_LeadsWithTheGestureAndTheDefaultActionThenTheDescription() {
        var entry = Row(ToolkitAction.Elevated("sfc", "/scannow"), description: "Scans system files");

        Assert.Equal("Double-click or press Enter: Run as administrator\nScans system files", entry.ActivationTip);
    }

    /// <summary>The tooltip does not carry the command, so the name has to, or every row of one kind
    /// would announce the same thing.</summary>
    [Fact]
    public void AccessibleName_NamesTheCommandAndItsDefaultAction() {
        var entry = Row(ToolkitAction.Capture("ipconfig", "/all"), command: "ipconfig /all");

        Assert.Equal("ipconfig /all: Run — output goes to the Execution Log", entry.AccessibleName);
    }

    /// <summary>Every row in both tables states its default; none falls through to a blank tooltip
    /// head.</summary>
    [Fact]
    public void DefaultActionText_EveryAuthoredRow_IsStated() {
        IEnumerable<ToolkitEntry> rows =
            [.. WindowsToolkitCatalog.Instance.Entries, .. LinuxToolkitCatalog.Instance.Entries];

        Assert.All(rows, entry => {
            Assert.False(string.IsNullOrWhiteSpace(entry.DefaultActionText));
            Assert.StartsWith(ToolkitEntry.ActivationGesture + ": " + entry.DefaultActionText + "\n",
                              entry.ActivationTip, StringComparison.Ordinal);
        });
    }

    /// <summary>Only a row whose default starts a program gets a play button; a folder or a link only
    /// navigates, and a double click or Enter covers that.</summary>
    [Theory]
    [InlineData(ToolkitActionKind.Launch, true)]
    [InlineData(ToolkitActionKind.Capture, true)]
    [InlineData(ToolkitActionKind.Elevated, true)]
    [InlineData(ToolkitActionKind.OpenPath, false)]
    [InlineData(ToolkitActionKind.OpenUrl, false)]
    public void HasRunButton_OnlyForRowsThatStartAProgram(ToolkitActionKind kind, bool expected) {
        var action = kind switch {
            ToolkitActionKind.Launch => ToolkitAction.Launch("regedit"),
            ToolkitActionKind.Capture => ToolkitAction.Capture("whoami"),
            ToolkitActionKind.Elevated => ToolkitAction.Elevated("sfc", "/scannow"),
            ToolkitActionKind.OpenPath => ToolkitAction.OpenPath(Folder),
            _ => ToolkitAction.OpenUrl("https://example.com"),
        };

        Assert.Equal(expected, Row(action).HasRunButton);
    }

    /// <summary>The play button names its action first and the command it acts on, so thirty of them
    /// do not all announce "Run".</summary>
    [Fact]
    public void RunButtonName_NamesTheActionAndTheCommand() =>
        Assert.Equal("Run as administrator: sfc /scannow",
                     Row(ToolkitAction.Elevated("sfc", "/scannow"), command: "sfc /scannow").RunButtonName);

    /// <summary>A row the user authored states its default like any catalog row does.</summary>
    [Theory]
    [InlineData(ToolkitCommandType.Url, "https://example.com", "Open in your browser")]
    [InlineData(ToolkitCommandType.Launch, "notepad", "Launch in its own window")]
    [InlineData(ToolkitCommandType.Capture, "whoami", "Run — output goes to the Execution Log")]
    public void DefaultActionText_UsersOwnCommand_FollowsItsType(ToolkitCommandType type, string payload, string expected) {
        var entry = ToolkitCommandFactory.ToEntry(new ToolkitCommand("Mine", "", type, payload, "", null));

        Assert.Equal(expected, entry.DefaultActionText);
    }
}
