using DashDetective.Tabs.Toolkit;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="SystemProcessLauncher.BuildLaunchInfo"/>: how a launch reaches the OS on each
/// platform. Nothing here starts a process — the decision is separated from the launch precisely so both
/// arms can be asserted from either dev machine, the <c>ToolkitPaths.Expand</c> shape.
/// </summary>
public class SystemProcessLauncherTests {
    private static ProcessStartInfoView Build(bool elevated, bool linux) {
        var info = SystemProcessLauncher.BuildLaunchInfo(
            "gnome-disks", ["--block-device", "/dev/sda"], elevated, linux);

        return new ProcessStartInfoView(
            info.FileName, [.. info.ArgumentList], info.UseShellExecute, info.Verb);
    }

    private sealed record ProcessStartInfoView(
        string FileName, string[] Arguments, bool UseShellExecute, string Verb);

    // ----- Windows: unchanged by this milestone -----

    /// <summary>The ordinary launch, on both platforms: the target stays the target and the shell
    /// resolves it, so associations and the PATH apply.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unelevated_HandsTheTargetToTheShellOnEitherPlatform(bool linux) {
        var info = Build(elevated: false, linux);

        Assert.Equal("gnome-disks", info.FileName);
        Assert.Equal(["--block-device", "/dev/sda"], info.Arguments);
        Assert.True(info.UseShellExecute);
        Assert.Equal("", info.Verb);
    }

    /// <summary>The Windows elevation path, pinned so the milestone stays a no-op there: the UAC verb on
    /// the target itself, still through the shell.</summary>
    [Fact]
    public void Elevated_OffLinux_UsesTheRunasVerbOnTheTarget() {
        var info = Build(elevated: true, linux: false);

        Assert.Equal("gnome-disks", info.FileName);
        Assert.Equal(["--block-device", "/dev/sda"], info.Arguments);
        Assert.True(info.UseShellExecute);
        Assert.Equal("runas", info.Verb);
    }

    // ----- Linux -----

    /// <summary>
    /// The milestone's central claim. Linux elevates through a wrapper program rather than a shell verb,
    /// so pkexec becomes the file name and the real target becomes its first argument — the target's own
    /// arguments following in order behind it.
    /// </summary>
    [Fact]
    public void Elevated_OnLinux_RunsTheTargetThroughPkexec() {
        var info = Build(elevated: true, linux: true);

        Assert.Equal(SystemProcessLauncher.ElevationProgram, info.FileName);
        Assert.Equal(["gnome-disks", "--block-device", "/dev/sda"], info.Arguments);
    }

    /// <summary>
    /// <c>UseShellExecute</c> has to be off for the elevated Linux launch. Left on, the launch goes to
    /// <c>xdg-open</c>, which cannot carry arguments — so the command would run with none of them, or
    /// not at all.
    /// </summary>
    [Fact]
    public void Elevated_OnLinux_DoesNotGoThroughTheShell() =>
        Assert.False(Build(elevated: true, linux: true).UseShellExecute);

    /// <summary>The <c>runas</c> verb has no meaning on Linux, and setting it alongside
    /// <c>UseShellExecute = false</c> throws when the process is started.</summary>
    [Fact]
    public void Elevated_OnLinux_SetsNoShellVerb() =>
        Assert.Equal("", Build(elevated: true, linux: true).Verb);

    /// <summary>The safety boundary, on the new path as much as the old: arguments reach the OS as a
    /// list, so there is no command line for a target or an argument to be injected into.</summary>
    [Fact]
    public void Elevated_OnLinux_KeepsArgumentsSeparateRatherThanJoined() {
        var info = SystemProcessLauncher.BuildLaunchInfo(
            "sh", ["-c", "echo hi; rm -rf /"], elevated: true, linux: true);

        Assert.Equal(["sh", "-c", "echo hi; rm -rf /"], info.ArgumentList.ToArray());
    }

    /// <summary>A target with no arguments is the common case for the catalog's elevated row.</summary>
    [Fact]
    public void Elevated_OnLinux_WorksWithNoArguments() {
        var info = SystemProcessLauncher.BuildLaunchInfo("fwupdmgr", [], elevated: true, linux: true);

        Assert.Equal(["fwupdmgr"], info.ArgumentList.ToArray());
    }
}
