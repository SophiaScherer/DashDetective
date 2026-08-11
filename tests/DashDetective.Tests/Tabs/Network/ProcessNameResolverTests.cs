using DashDetective.Tabs.Network;
using DashDetective.Tests.Fakes;
using System;
using System.Diagnostics;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Covers the two <see cref="IProcessNameResolver"/> arms. Naming a process is not portable even
/// though looking one up is, which is why this is a seam at all: the <c>.exe</c> suffix and the meaning of
/// PIDs 0 and 4 are Windows facts that would quietly mislabel real Linux rows.</summary>
public class ProcessNameResolverTests {
    /// <summary>Linux reads the command line, so a name longer than the kernel's 15-char <c>comm</c> limit
    /// survives intact — the difference the shared derivation exists to guarantee.</summary>
    [Fact]
    public void Linux_ReadsTheFullNameFromProc() {
        var proc = new FakeProcFileSystem()
            .WithFile("/proc/812/cmdline", "/usr/lib/systemd/systemd-resolved\0")
            .WithFile("/proc/812/comm", "systemd-resolve\n");

        Assert.Equal("systemd-resolved", new LinuxProcessNameResolver(proc).Resolve(812));
    }

    /// <summary>No <c>.exe</c> on Linux. Appending it is a Windows convention, and nothing in the table
    /// depends on an extension being there.</summary>
    [Fact]
    public void Linux_AppendsNoExeSuffix() {
        var proc = new FakeProcFileSystem().WithFile("/proc/1201/cmdline", "/usr/bin/firefox\0");

        Assert.Equal("firefox", new LinuxProcessNameResolver(proc).Resolve(1201));
    }

    /// <summary>PID 0 is not a process — it is the sentinel for a socket whose owner could not be seen,
    /// which is what another user's connection looks like to an unprivileged reader. Naming it "System
    /// Idle" the way Windows does would claim an owner that does not exist.</summary>
    [Fact]
    public void Linux_UnattributedSocket_ShowsThePlaceholderDash() =>
        Assert.Equal("—", new LinuxProcessNameResolver(new FakeProcFileSystem()).Resolve(0));

    /// <summary>PID 4 is an ordinary kernel thread on Linux, not the System process. Carrying the Windows
    /// well-known table across would mislabel a real row.</summary>
    [Fact]
    public void Linux_HasNoWellKnownPids() {
        var proc = new FakeProcFileSystem().WithFile("/proc/4/comm", "kworker/0:0H\n");

        Assert.Equal("kworker/0:0H", new LinuxProcessNameResolver(proc).Resolve(4));
    }

    /// <summary>A process that exited between the socket read and the name read denies both files, and the
    /// PID is still worth showing — it is enough to find the process with another tool.</summary>
    [Fact]
    public void Linux_UnreadableProcess_FallsBackToThePid() =>
        Assert.Equal("PID 9999", new LinuxProcessNameResolver(new FakeProcFileSystem()).Resolve(9999));

    /// <summary>The Windows well-known PIDs, which own sockets but are not ordinary processes.</summary>
    [Theory]
    [InlineData(0, "System Idle")]
    [InlineData(4, "System")]
    public void Windows_NamesTheWellKnownPids(int pid, string expected) {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal(expected, new WindowsProcessNameResolver().Resolve(pid));
    }

    /// <summary>The suffix the rest of the Windows UI uses. Asserted against this process, so it holds
    /// without depending on anything else being running.</summary>
    [Fact]
    public void Windows_AppendsTheExeSuffix() {
        if (!OperatingSystem.IsWindows())
            return;

        using var self = Process.GetCurrentProcess();

        Assert.Equal(self.ProcessName + ".exe", new WindowsProcessNameResolver().Resolve(self.Id));
    }

    /// <summary>An exited or protected process throws out of the lookup; the resolver contains it.</summary>
    [Fact]
    public void Windows_UnreadableProcess_FallsBackToThePid() {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal("PID 999999", new WindowsProcessNameResolver().Resolve(999999));
    }

    /// <summary>Both arms and the unsupported one word an unnamed process identically, so a user comparing
    /// two machines does not see two different placeholders for the same situation.</summary>
    [Fact]
    public void EveryArm_WordsAnUnnamedProcessTheSameWay() {
        Assert.Equal("PID 77", IProcessNameResolver.Unnamed(77));
        Assert.Equal("PID 77", new UnsupportedProcessNameResolver().Resolve(77));
        Assert.Equal("PID 77", new LinuxProcessNameResolver(new FakeProcFileSystem()).Resolve(77));
    }

    // ----- Reader identity -----

    /// <summary>The arm a green Windows run never executes. Grep the suite for
    /// <c>OperatingSystem.IsLinux</c> after touching any factory — this is why.</summary>
    [Fact]
    public void ForCurrentPlatform_PicksThisPlatformsResolver() {
        var resolver = IProcessNameResolver.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsProcessNameResolver>(resolver);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxProcessNameResolver>(resolver);
        else
            Assert.IsType<UnsupportedProcessNameResolver>(resolver);
    }

    /// <summary>The Linux arm's construction path, exercised from ANY host — the assertion above only runs
    /// it on Linux. On a box with no <c>/proc</c> every PID is simply unnamed.</summary>
    [Fact]
    public void LinuxResolver_ConstructsAndDegradesOnAnyHost() {
        var resolver = new LinuxProcessNameResolver();

        if (!OperatingSystem.IsLinux())
            Assert.Equal("PID 812", resolver.Resolve(812));
    }
}
