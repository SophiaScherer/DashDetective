using DashDetective.Tabs.Toolkit;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitAction"/>: each factory picks the right path, only a captured
/// action redirects output, elevation never implies capture (Windows forbids it), and the argument list
/// stays a list — the property the Toolkit's safety rests on.</summary>
public class ToolkitActionTests {
    [Fact]
    public void OpenPath_TakesTheShellPathWithNoArguments() {
        var action = ToolkitAction.OpenPath("%appdata%");

        Assert.Equal(ToolkitActionKind.OpenPath, action.Kind);
        Assert.Equal("%appdata%", action.Target);
        Assert.Empty(action.Arguments);
        Assert.False(action.CapturesOutput);
        Assert.False(action.RequiresElevation);
    }

    [Fact]
    public void Capture_IsTheOnlyKindThatRedirectsOutput() {
        Assert.True(ToolkitAction.Capture("ipconfig", "/all").CapturesOutput);

        Assert.False(ToolkitAction.OpenPath("%temp%").CapturesOutput);
        Assert.False(ToolkitAction.OpenUrl("https://example.com").CapturesOutput);
        Assert.False(ToolkitAction.Launch("regedit").CapturesOutput);
    }

    /// <summary>Windows refuses to redirect the streams of a <c>runas</c> process, so an elevated action
    /// must never claim it captures — the two are separate kinds precisely so this cannot be expressed.</summary>
    [Fact]
    public void Elevated_RequiresElevationAndNeverCaptures() {
        var action = ToolkitAction.Elevated("sfc", "/scannow");

        Assert.True(action.RequiresElevation);
        Assert.False(action.CapturesOutput);
    }

    [Fact]
    public void Factories_OtherThanElevated_DoNotRequireElevation() {
        Assert.False(ToolkitAction.OpenPath("%windir%").RequiresElevation);
        Assert.False(ToolkitAction.OpenUrl("https://example.com").RequiresElevation);
        Assert.False(ToolkitAction.Launch("regedit").RequiresElevation);
        Assert.False(ToolkitAction.Capture("systeminfo").RequiresElevation);
    }

    [Fact]
    public void Capture_KeepsArgumentsSeparateRatherThanJoiningThem() {
        var action = ToolkitAction.Capture("ipconfig", "/all", "/extra");

        Assert.Equal(["/all", "/extra"], action.Arguments);
    }

    [Fact]
    public void DefaultTimeout_AppliesUntilOverridden() {
        var action = ToolkitAction.Capture("systeminfo");

        Assert.Equal(ToolkitAction.DefaultTimeout, action.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(90), action.WithTimeout(TimeSpan.FromSeconds(90)).Timeout);
    }

    [Fact]
    public void WithTimeout_LeavesEverythingElseAlone() {
        var action = ToolkitAction.Capture("ipconfig", "/all");
        var slower = action.WithTimeout(TimeSpan.FromSeconds(60));

        Assert.Equal(action.Kind, slower.Kind);
        Assert.Equal(action.Target, slower.Target);
        Assert.Equal(action.Arguments, slower.Arguments);
    }

    /// <summary>A parameterised entry's value becomes exactly one element, so however it is spelled it
    /// cannot split into a second argument or turn into a flag.</summary>
    [Fact]
    public void WithArgument_AppendsExactlyOneElementHoweverItIsSpelled() {
        var action = ToolkitAction.Capture("ping", "-n", "4").WithArgument("a b -c \"d\"");

        Assert.Equal(["-n", "4", "a b -c \"d\""], action.Arguments);
    }

    [Fact]
    public void WithArgument_LeavesTheOriginalUntouched() {
        var action = ToolkitAction.Capture("ping", "-n", "4");

        action.WithArgument("example.com");

        Assert.Equal(["-n", "4"], action.Arguments);
    }
}
