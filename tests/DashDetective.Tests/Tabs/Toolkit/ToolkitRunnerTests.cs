using DashDetective.Tabs.Toolkit;
using DashDetective.Tests.Fakes;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>Covers <see cref="ToolkitRunner"/>: each action kind takes the right path, arguments reach
/// the OS as a list rather than a command line, a non-https documentation link is refused outright, and
/// every way a run can go wrong — missing tool, non-zero exit, timeout, declined UAC prompt — becomes a
/// worded failure result instead of an exception.</summary>
public class ToolkitRunnerTests {
    private static (ToolkitRunner Runner, FakeProcessLauncher Launcher) Build() {
        var launcher = new FakeProcessLauncher();
        return (new ToolkitRunner(launcher), launcher);
    }

    // ----- Routing -----

    [Fact]
    public async Task RunAsync_OpenPath_ShellLaunchesWithoutCapturingOrElevating() {
        var (runner, launcher) = Build();

        var result = await runner.RunAsync(ToolkitAction.OpenPath(@"C:\Windows"));

        Assert.True(result.Success);
        Assert.Equal(ToolkitOutputFormatter.Opened, result.Output);
        Assert.False(launcher.Single.Captured);
        Assert.False(launcher.Single.Elevated);
        Assert.Equal(@"C:\Windows", launcher.Single.FileName);
    }

    [Fact]
    public async Task RunAsync_Launch_ReportsThatTheToolStarted() {
        var (runner, launcher) = Build();

        var result = await runner.RunAsync(ToolkitAction.Launch("regedit"));

        Assert.True(result.Success);
        Assert.Equal(ToolkitOutputFormatter.Launched, result.Output);
        Assert.False(launcher.Single.Captured);
    }

    [Fact]
    public async Task RunAsync_Elevated_UsesTheElevatedLaunchAndSaysOutputIsNotCaptured() {
        var (runner, launcher) = Build();

        var result = await runner.RunAsync(ToolkitAction.Elevated("sfc", "/scannow"));

        Assert.True(result.Success);
        Assert.Equal(ToolkitOutputFormatter.LaunchedElevated, result.Output);
        Assert.True(launcher.Single.Elevated);
        Assert.False(launcher.Single.Captured);
    }

    [Fact]
    public async Task RunAsync_Capture_TakesTheRedirectedPathWithTheActionsTimeout() {
        var (runner, launcher) = Build();
        launcher.NextCapture = new ProcessCapture(0, "Windows IP Configuration", "", false);

        var result = await runner.RunAsync(
            ToolkitAction.Capture("ipconfig", "/all").WithTimeout(TimeSpan.FromSeconds(45)));

        Assert.True(result.Success);
        Assert.Equal("Windows IP Configuration", result.Output);
        Assert.True(launcher.Single.Captured);
        Assert.Equal(TimeSpan.FromSeconds(45), launcher.Single.Timeout);
    }

    /// <summary>The safety property: arguments reach the OS as separate elements, so there is no command
    /// line for anything to be interpolated into.</summary>
    [Fact]
    public async Task RunAsync_PassesArgumentsThroughUnjoined() {
        var (runner, launcher) = Build();

        await runner.RunAsync(ToolkitAction.Capture("ping", "-n", "4").WithArgument("a b & c"));

        Assert.Equal(["-n", "4", "a b & c"], launcher.Single.Arguments);
    }

    /// <summary>Each platform in its own notation: "%windir%" on Windows, "~" elsewhere. Both go
    /// through ToolkitPaths.Resolve, so this covers the wiring rather than the expansion itself.</summary>
    [Fact]
    public async Task RunAsync_ExpandsEnvironmentVariablesInTheTargetAtRunTime() {
        var (runner, launcher) = Build();
        var windows = OperatingSystem.IsWindows();

        await runner.RunAsync(ToolkitAction.OpenPath(windows ? "%windir%" : "~"));

        var expected = Environment.GetFolderPath(
            windows ? Environment.SpecialFolder.Windows : Environment.SpecialFolder.UserProfile);

        Assert.Equal(expected, launcher.Single.FileName, ignoreCase: true);
    }

    /// <summary>A user-supplied value stays literal — expansion is for the catalog's own targets only.</summary>
    [Fact]
    public async Task RunAsync_DoesNotExpandEnvironmentVariablesInArguments() {
        var (runner, launcher) = Build();

        await runner.RunAsync(ToolkitAction.Capture("ping").WithArgument("%windir%"));

        Assert.Equal(["%windir%"], launcher.Single.Arguments);
    }

    // ----- The URL guard -----

    [Fact]
    public async Task RunAsync_HttpsUrl_IsOpened() {
        var (runner, launcher) = Build();

        var result = await runner.RunAsync(ToolkitAction.OpenUrl("https://learn.microsoft.com"));

        Assert.True(result.Success);
        Assert.Equal("https://learn.microsoft.com", launcher.Single.FileName);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    public async Task RunAsync_NonHttpsUrl_IsRefusedWithoutStartingAnything(string target) {
        var (runner, launcher) = Build();

        var result = await runner.RunAsync(ToolkitAction.OpenUrl(target));

        Assert.False(result.Success);
        Assert.Contains(target, result.Output, StringComparison.Ordinal);
        Assert.Empty(launcher.Calls);
    }

    [Fact]
    public async Task RunAsync_UrlSchemeCheckIgnoresCase() {
        var (runner, _) = Build();

        Assert.True((await runner.RunAsync(ToolkitAction.OpenUrl("HTTPS://learn.microsoft.com"))).Success);
    }

    // ----- Failure paths -----

    [Fact]
    public async Task RunAsync_LaunchThrows_BecomesAWordedFailureRatherThanAnException() {
        var (runner, launcher) = Build();
        launcher.ThrowOnCall = new FileNotFoundException("The system cannot find the file specified");

        var result = await runner.RunAsync(ToolkitAction.Launch("nosuchtool.exe"));

        Assert.False(result.Success);
        Assert.Contains("cannot find the file", result.Output, StringComparison.Ordinal);
    }

    /// <summary>Dismissing the UAC prompt is a decision, not a fault, and reads as one in the log.</summary>
    [Fact]
    public async Task RunAsync_ElevationDeclined_ReadsAsCancelledNotAsAnError() {
        var (runner, launcher) = Build();
        launcher.ThrowOnCall = new Win32Exception(1223);

        var result = await runner.RunAsync(ToolkitAction.Elevated("sfc", "/scannow"));

        Assert.False(result.Success);
        Assert.Equal(ToolkitOutputFormatter.ElevationCancelled, result.Output);
    }

    [Fact]
    public async Task RunAsync_OtherWin32Failure_IsNotMistakenForACancelledPrompt() {
        var (runner, launcher) = Build();
        launcher.ThrowOnCall = new Win32Exception(5); // ERROR_ACCESS_DENIED

        var result = await runner.RunAsync(ToolkitAction.Elevated("sfc", "/scannow"));

        Assert.False(result.Success);
        Assert.NotEqual(ToolkitOutputFormatter.ElevationCancelled, result.Output);
    }

    [Fact]
    public async Task RunAsync_CaptureTimesOut_FailsAndKeepsWhatWasPrinted() {
        var (runner, launcher) = Build();
        launcher.NextCapture = new ProcessCapture(-1, "got this far", "", TimedOut: true);

        var result = await runner.RunAsync(
            ToolkitAction.Capture("systeminfo").WithTimeout(TimeSpan.FromSeconds(20)));

        Assert.False(result.Success);
        Assert.Contains("got this far", result.Output, StringComparison.Ordinal);
        Assert.Contains("20s", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_CaptureExitsNonZero_FailsAndCarriesTheCode() {
        var (runner, launcher) = Build();
        launcher.NextCapture = new ProcessCapture(2, "", "Bad option", false);

        var result = await runner.RunAsync(ToolkitAction.Capture("ipconfig", "/nope"));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Bad option", result.Output, StringComparison.Ordinal);
        Assert.Contains("code 2", result.Output, StringComparison.Ordinal);
    }

    /// <summary>A clean run that printed nothing is still a success, and must not leave the log looking
    /// like the button did nothing.</summary>
    [Fact]
    public async Task RunAsync_CaptureSucceedsSilently_SaysSoRatherThanShowingNothing() {
        var (runner, launcher) = Build();
        launcher.NextCapture = new ProcessCapture(0, "   \r\n ", "", false);

        var result = await runner.RunAsync(ToolkitAction.Capture("ipconfig", "/flushdns"));

        Assert.True(result.Success);
        Assert.Equal(ToolkitOutputFormatter.NoOutput, result.Output);
    }

    [Fact]
    public async Task RunAsync_CaptureThrows_BecomesAWordedFailure() {
        var (runner, launcher) = Build();
        launcher.ThrowOnCall = new InvalidOperationException("Could not start systeminfo.");

        var result = await runner.RunAsync(ToolkitAction.Capture("systeminfo"));

        Assert.False(result.Success);
        Assert.Contains("Could not start", result.Output, StringComparison.Ordinal);
    }
}
