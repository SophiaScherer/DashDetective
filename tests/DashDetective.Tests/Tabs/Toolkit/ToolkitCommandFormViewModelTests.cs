using DashDetective.Tabs.Toolkit;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers the "+ Add command" form: what it collects, when it refuses, and that a refusal leaves the
/// user one correction away rather than one re-type away.
/// </summary>
public class ToolkitCommandFormViewModelTests {
    private readonly List<ToolkitCommand> _saved = [];
    private readonly List<ToolkitEntry> _existing = [];

    private ToolkitCommandFormViewModel Form() =>
        new(() => _existing, _saved.Add);

    private static ToolkitCommandFormViewModel Filled(
        ToolkitCommandFormViewModel form, ToolkitCommandType type, string title, string payload) {
        form.Types.First(t => t.Type == type).SelectCommand.Execute(null);
        form.Title = title;
        form.Payload = payload;
        return form;
    }

    [Fact]
    public void Constructor_StartsClosedOnTheFirstTypeAndNoCategory() {
        var form = Form();

        Assert.False(form.IsOpen);
        Assert.Equal(ToolkitCommandType.FolderPath, form.SelectedType);
        Assert.True(form.Types[0].IsSelected);
        Assert.Null(form.SelectedCategory);
        Assert.True(form.Categories[0].IsSelected);
        Assert.False(form.HasError);
    }

    /// <summary>Every command this form makes is already in My Commands, so offering it as the "also show
    /// under" choice would read as a pick that does nothing.</summary>
    [Fact]
    public void Categories_OfferNoneAndEverySectionExceptCustom() {
        var form = Form();

        Assert.Equal("None", form.Categories[0].Label);
        Assert.Null(form.Categories[0].Category);
        Assert.DoesNotContain(form.Categories, c => c.Category == ToolkitCategory.Custom);
        Assert.Equal(ToolkitCatalog.Categories.Count, form.Categories.Count);
    }

    [Fact]
    public void Types_OfferEveryTypeAndNoneOfThemElevated() {
        var form = Form();

        Assert.Equal(
            System.Enum.GetValues<ToolkitCommandType>(), form.Types.Select(t => t.Type));
    }

    [Fact]
    public void Open_ShowsTheFormOnEmptyFields() {
        var form = Form();
        form.Title = "left over";

        form.OpenCommand.Execute(null);

        Assert.True(form.IsOpen);
        Assert.Equal("", form.Title);
    }

    [Fact]
    public void Cancel_ClosesAndThrowsAwayWhatWasTyped() {
        var form = Form();
        form.OpenCommand.Execute(null);
        form.Title = "half typed";

        form.CancelCommand.Execute(null);

        Assert.False(form.IsOpen);
        Assert.Equal("", form.Title);
        Assert.Empty(_saved);
    }

    // ----- The payload field follows the type -----

    [Theory]
    [InlineData(ToolkitCommandType.FolderPath, "Folder path", false)]
    [InlineData(ToolkitCommandType.Url, "Address", false)]
    [InlineData(ToolkitCommandType.Launch, "Program", true)]
    [InlineData(ToolkitCommandType.Capture, "Program", true)]
    public void SelectedType_DrivesThePayloadLabelAndWhetherArgumentsApply(
        ToolkitCommandType type, string label, bool takesArguments) {
        var form = Form();

        form.Types.First(t => t.Type == type).SelectCommand.Execute(null);

        Assert.Equal(label, form.PayloadLabel);
        Assert.Equal(takesArguments, form.TakesArguments);
        Assert.False(string.IsNullOrWhiteSpace(form.PayloadPrompt));
    }

    [Fact]
    public void SelectType_MarksExactlyOneChip() {
        var form = Form();

        form.Types.First(t => t.Type == ToolkitCommandType.Capture).SelectCommand.Execute(null);

        Assert.Single(form.Types, t => t.IsSelected);
        Assert.Equal(ToolkitCommandType.Capture, form.Types.Single(t => t.IsSelected).Type);
    }

    [Fact]
    public void SelectCategory_MarksExactlyOneChipAndSetsTheLabel() {
        var form = Form();

        form.Categories.First(c => c.Category == ToolkitCategory.Diagnostics)
            .SelectCommand.Execute(null);

        Assert.Single(form.Categories, c => c.IsSelected);
        Assert.Equal(ToolkitCategory.Diagnostics, form.SelectedCategory);
    }

    // ----- Saving -----

    [Fact]
    public void Save_AWellFormedCommand_HandsItOnAndClosesTheForm() {
        var form = Form();
        form.OpenCommand.Execute(null);
        Filled(form, ToolkitCommandType.Capture, "Ports", "netstat");
        form.Description = "Listening sockets";
        form.Arguments = "-an";
        form.Categories.First(c => c.Category == ToolkitCategory.Diagnostics)
            .SelectCommand.Execute(null);

        form.SaveCommand.Execute(null);

        var saved = Assert.Single(_saved);
        Assert.Equal(
            new ToolkitCommand("Ports", "Listening sockets", ToolkitCommandType.Capture, "netstat",
                               "-an", ToolkitCategory.Diagnostics),
            saved);
        Assert.False(form.IsOpen);
        Assert.Equal("", form.Title);
    }

    [Fact]
    public void Save_TrimsEveryFieldOnTheWayOut() {
        var form = Form();
        Filled(form, ToolkitCommandType.Launch, "  Mine  ", "  thing.exe  ");
        form.Arguments = "  /quiet  ";

        form.SaveCommand.Execute(null);

        var saved = Assert.Single(_saved);
        Assert.Equal("Mine", saved.Title);
        Assert.Equal("thing.exe", saved.Payload);
        Assert.Equal("/quiet", saved.Arguments);
    }

    /// <summary>A folder takes no arguments, so anything typed while another type was picked must not
    /// ride along invisibly on the saved command.</summary>
    [Fact]
    public void Save_DropsArgumentsTypedForATypeThatDoesNotTakeThem() {
        var form = Form();
        Filled(form, ToolkitCommandType.Capture, "Mine", "netstat");
        form.Arguments = "-an";
        form.Types.First(t => t.Type == ToolkitCommandType.FolderPath).SelectCommand.Execute(null);
        form.Payload = @"C:\work";

        form.SaveCommand.Execute(null);

        Assert.Equal("", Assert.Single(_saved).Arguments);
    }

    [Fact]
    public void Save_NoTitle_RefusesAndSaysWhy() {
        var form = Form();
        form.OpenCommand.Execute(null);
        form.Payload = @"C:\work";

        form.SaveCommand.Execute(null);

        Assert.Empty(_saved);
        Assert.True(form.HasError);
        Assert.Equal(ToolkitCommandValidator.TitleRequired, form.Error);
        Assert.True(form.IsOpen);
    }

    /// <summary>A refusal must not cost the user what they typed — they are one correction away.</summary>
    [Fact]
    public void Save_Refused_KeepsEveryFieldWhereItIs() {
        var form = Form();
        form.OpenCommand.Execute(null);
        Filled(form, ToolkitCommandType.Url, "Docs", "http://example.com");
        form.Description = "worth keeping";

        form.SaveCommand.Execute(null);

        Assert.Equal(ToolkitCommandValidator.UrlMustBeHttps, form.Error);
        Assert.Equal("Docs", form.Title);
        Assert.Equal("http://example.com", form.Payload);
        Assert.Equal("worth keeping", form.Description);
    }

    [Fact]
    public void Save_TitleAlreadyOnThePage_IsRefused() {
        _existing.AddRange(ToolkitCatalog.Entries);
        var form = Form();
        Filled(form, ToolkitCommandType.FolderPath, "%temp%", @"C:\work");

        form.SaveCommand.Execute(null);

        Assert.Empty(_saved);
        Assert.Equal(ToolkitCommandValidator.TitleTaken, form.Error);
    }

    /// <summary>Once the field a refusal named has been touched, the message is about a state that no
    /// longer exists.</summary>
    [Theory]
    [InlineData("title")]
    [InlineData("payload")]
    [InlineData("type")]
    public void Error_ClearsAsSoonAsTheUserChangesSomething(string field) {
        var form = Form();
        form.SaveCommand.Execute(null);
        Assert.True(form.HasError);

        switch (field) {
            case "title": form.Title = "t"; break;
            case "payload": form.Payload = "p"; break;
            default:
                form.Types.First(t => t.Type == ToolkitCommandType.Url).SelectCommand.Execute(null);
                break;
        }

        Assert.False(form.HasError);
    }

    [Fact]
    public void Save_ThenOpenAgain_StartsCleanIncludingTheChips() {
        var form = Form();
        Filled(form, ToolkitCommandType.Capture, "Ports", "netstat");
        form.Categories.First(c => c.Category == ToolkitCategory.Diagnostics)
            .SelectCommand.Execute(null);
        form.SaveCommand.Execute(null);

        form.OpenCommand.Execute(null);

        Assert.Equal(ToolkitCommandType.FolderPath, form.SelectedType);
        Assert.Null(form.SelectedCategory);
        Assert.True(form.Types[0].IsSelected);
        Assert.True(form.Categories[0].IsSelected);
    }

    // ----- Wired to the page -----

    /// <summary>The page's own form saves onto the page: a command added here is a row, is persisted, and
    /// is refused a second time for the title it now holds.</summary>
    [Fact]
    public void PageForm_SavesOntoThePageAndThenGuardsItsOwnTitle() {
        var vm = new ToolkitViewModel();
        var announced = 0;
        vm.CommandsChanged += () => announced++;

        Filled(vm.Form, ToolkitCommandType.Launch, "zzz-my-own", "thing.exe");
        vm.Form.SaveCommand.Execute(null);

        Assert.Equal("zzz-my-own", Assert.Single(vm.Custom).Command);
        Assert.Equal(1, announced);

        Filled(vm.Form, ToolkitCommandType.Launch, "zzz-my-own", "other.exe");
        vm.Form.SaveCommand.Execute(null);

        Assert.Single(vm.Custom);
        Assert.Equal(ToolkitCommandValidator.TitleTaken, vm.Form.Error);
    }
}
