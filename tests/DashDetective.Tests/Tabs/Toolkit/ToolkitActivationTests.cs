using DashDetective.Tabs.Toolkit;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Pins <see cref="ToolkitActivation"/>: Enter runs a Toolkit row only when a row body has focus (that
/// row) or nothing does (the selection) — focus anywhere else, on the page or in the shell, keeps the key —
/// and a double click on the play button counts once.
/// </summary>
public class ToolkitActivationTests {
    private static ToolkitEntry Row(string command) =>
        new(command, "", ToolkitCategory.Diagnostics, ToolkitEntryKind.Command, ToolkitAction.Capture(command));

    [Fact]
    public void EnterTarget_NothingFocused_IsTheSelection() {
        var selected = Row("selected");

        Assert.Same(selected, ToolkitActivation.EnterTarget(ToolkitActivation.Focus.Nothing, null, selected));
    }

    [Fact]
    public void EnterTarget_ARowBodyFocused_IsThatRowNotTheSelection() {
        var focused = Row("focused");

        Assert.Same(focused, ToolkitActivation.EnterTarget(ToolkitActivation.Focus.RowBody, focused, Row("selected")));
    }

    /// <summary>Focus outside the page (a toolbar button, a nav item) or on another of its buttons keeps
    /// Enter for that control, whatever is selected.</summary>
    [Fact]
    public void EnterTarget_FocusOnAnythingElse_RunsNothing() =>
        Assert.Null(ToolkitActivation.EnterTarget(ToolkitActivation.Focus.Other, null, Row("selected")));

    [Fact]
    public void EnterTarget_NothingFocusedAndNothingSelected_RunsNothing() =>
        Assert.Null(ToolkitActivation.EnterTarget(ToolkitActivation.Focus.Nothing, null, null));

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void IsRepeatPress_OnlyTheFirstPressOfAMultiClickCounts(int clickCount, bool expected) =>
        Assert.Equal(expected, ToolkitActivation.IsRepeatPress(clickCount));
}
