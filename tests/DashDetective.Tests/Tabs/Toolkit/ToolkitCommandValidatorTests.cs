using DashDetective.Tabs.Toolkit;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="ToolkitCommandValidator"/>: what the "+ Add command" form accepts, and what it says
/// when it does not. The https rule is the one with teeth behind it — the runner refuses anything else
/// regardless, so this is the same answer given while it is still fixable.
/// </summary>
public class ToolkitCommandValidatorTests {
    private static ToolkitCommand Folder(string title = "My folder", string payload = @"C:\work") =>
        new(title, "", ToolkitCommandType.FolderPath, payload);

    [Fact]
    public void Validate_AWellFormedCommand_IsAccepted() {
        Assert.Null(ToolkitCommandValidator.Validate(Folder(), []));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NoTitle_IsRefused(string title) {
        Assert.Equal(ToolkitCommandValidator.TitleRequired,
                     ToolkitCommandValidator.Validate(Folder(title), []));
    }

    /// <summary>Pins and search reveal are keyed by command text, so two rows sharing a title would make
    /// both ambiguous.</summary>
    [Fact]
    public void Validate_TitleAlreadyOnThePage_IsRefused() {
        Assert.Equal(ToolkitCommandValidator.TitleTaken,
                     ToolkitCommandValidator.Validate(Folder("%temp%"), WindowsToolkitCatalog.Instance.Entries));
    }

    [Fact]
    public void Validate_TitleClashIsCaseInsensitive() {
        Assert.Equal(ToolkitCommandValidator.TitleTaken,
                     ToolkitCommandValidator.Validate(Folder("REGEDIT"), WindowsToolkitCatalog.Instance.Entries));
    }

    [Fact]
    public void Validate_TitleWithSurroundingSpaceStillClashes() {
        Assert.Equal(ToolkitCommandValidator.TitleTaken,
                     ToolkitCommandValidator.Validate(Folder("  regedit  "), WindowsToolkitCatalog.Instance.Entries));
    }

    /// <summary>Editing a row without renaming it is not a clash with itself.</summary>
    [Fact]
    public void Validate_EditingACommandKeepingItsOwnTitle_IsAccepted() {
        var original = Folder("Mine");
        var entry = ToolkitCommandFactory.ToEntry(original);
        var edited = original with { Payload = @"C:\somewhere-else" };

        Assert.Null(ToolkitCommandValidator.Validate(edited, [entry], replacing: original));
    }

    /// <summary>...but renaming onto a title someone else holds still is.</summary>
    [Fact]
    public void Validate_EditingOntoAnotherRowsTitle_IsRefused() {
        var original = Folder("Mine");
        var entry = ToolkitCommandFactory.ToEntry(original);
        var other = ToolkitCommandFactory.ToEntry(Folder("Theirs"));

        Assert.Equal(
            ToolkitCommandValidator.TitleTaken,
            ToolkitCommandValidator.Validate(
                original with { Title = "Theirs" }, [entry, other], replacing: original));
    }

    // ----- Payloads -----

    [Fact]
    public void Validate_FolderWithNoPath_IsRefused() {
        Assert.Equal(ToolkitCommandValidator.PathRequired,
                     ToolkitCommandValidator.Validate(Folder(payload: "  "), []));
    }

    /// <summary>OpenPath hands the target to the shell, which would happily open a URL from a row badged
    /// as a folder — the label would be lying about what the row does.</summary>
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    public void Validate_FolderPathThatIsReallyALink_IsRefused(string payload) {
        Assert.Equal(ToolkitCommandValidator.PathCannotBeUrl,
                     ToolkitCommandValidator.Validate(Folder(payload: payload), []));
    }

    [Fact]
    public void Validate_ProgramWithNoName_IsRefused() {
        Assert.Equal(
            ToolkitCommandValidator.ProgramRequired,
            ToolkitCommandValidator.Validate(
                new ToolkitCommand("Run it", "", ToolkitCommandType.Launch, ""), []));
        Assert.Equal(
            ToolkitCommandValidator.ProgramRequired,
            ToolkitCommandValidator.Validate(
                new ToolkitCommand("Capture it", "", ToolkitCommandType.Capture, "   "), []));
    }

    [Fact]
    public void Validate_UrlWithNoAddress_IsRefused() {
        Assert.Equal(ToolkitCommandValidator.UrlRequired,
                     ToolkitCommandValidator.Validate(
                         new ToolkitCommand("Docs", "", ToolkitCommandType.Url, ""), []));
    }

    /// <summary>The scheme rule the runner enforces anyway, said early: http, file and the custom
    /// protocol handlers all stop here.</summary>
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("file:///C:/Windows")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:privacy")]
    [InlineData("example.com")]
    public void Validate_UrlThatIsNotHttps_IsRefused(string payload) {
        Assert.Equal(ToolkitCommandValidator.UrlMustBeHttps,
                     ToolkitCommandValidator.Validate(
                         new ToolkitCommand("Docs", "", ToolkitCommandType.Url, payload), []));
    }

    [Fact]
    public void Validate_HttpsUrl_IsAccepted() {
        Assert.Null(ToolkitCommandValidator.Validate(
            new ToolkitCommand("Docs", "", ToolkitCommandType.Url, "HTTPS://example.com"), []));
    }

    /// <summary>A description is the one field the form does not insist on — a bare row is a choice, not
    /// a mistake.</summary>
    [Fact]
    public void Validate_NoDescription_IsAccepted() {
        Assert.Null(ToolkitCommandValidator.Validate(
            new ToolkitCommand("Mine", "", ToolkitCommandType.Launch, "thing.exe"), []));
    }

    [Fact]
    public void Normalize_TrimsEveryFreeTextFieldAndLeavesTheRest() {
        var normalized = ToolkitCommandValidator.Normalize(new ToolkitCommand(
            "  Mine  ", "  what it does  ", ToolkitCommandType.Capture, "  netstat  ", "  -an  ",
            ToolkitCategory.Diagnostics));

        Assert.Equal("Mine", normalized.Title);
        Assert.Equal("what it does", normalized.Description);
        Assert.Equal("netstat", normalized.Payload);
        Assert.Equal("-an", normalized.Arguments);
        Assert.Equal(ToolkitCategory.Diagnostics, normalized.Category);
    }
}
