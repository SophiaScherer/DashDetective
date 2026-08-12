using DashDetective.Tabs.Processes;
using DashDetective.Tests.Fakes;
using System;
using System.Diagnostics;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>
/// Covers <see cref="LinuxProcessInterop"/>: which folder the Properties button reveals for a process,
/// and the deliberately empty I/O contract.
///
/// The lookup runs over <see cref="FakeProcFileSystem"/>, so it all runs on a Windows dev machine.
/// <c>ShowProperties</c>' launch is not exercised — it starts a file manager.
/// </summary>
public class LinuxProcessInteropTests {
    /// <summary>A process whose <c>/proc/[pid]/exe</c> resolves, built through
    /// <see cref="TestPaths"/> because the subject calls <c>Path.GetDirectoryName</c>, which normalises
    /// its result to the running host's separator.</summary>
    private static LinuxProcessInterop Interop() =>
        new(new FakeProcFileSystem().WithLink("/proc/812/exe", TestPaths.Of("usr", "bin", "gedit")));

    [Fact]
    public void ForCurrentPlatform_ResolvesTheInteropForThisHost() {
        var interop = IProcessInterop.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsProcessInterop>(interop);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxProcessInterop>(interop);
        else
            Assert.IsType<UnsupportedProcessInterop>(interop);
    }

    /// <summary>The executable's folder, read from the <c>/proc/[pid]/exe</c> symlink.</summary>
    [Fact]
    public void RevealTarget_IsTheFolderHoldingTheExecutable() =>
        Assert.Equal(TestPaths.Of("usr", "bin"), Interop().RevealTarget(812));

    /// <summary>
    /// The two ordinary cases that resolve to nothing: a kernel thread has no executable, and another
    /// user's <c>exe</c> link is owner-only. Neither is a failure — both simply reveal nothing rather
    /// than opening the wrong folder.
    /// </summary>
    [Fact]
    public void RevealTarget_IsNullWhenTheExeLinkCannotBeRead() =>
        Assert.Null(Interop().RevealTarget(999));

    /// <summary>A process that exited between the row being drawn and the button being clicked. The
    /// code-behind calls this unconditionally on the selected row, so it must not throw.</summary>
    [Fact]
    public void ShowProperties_DoesNotThrowForAnUnknownPid() =>
        Interop().ShowProperties(IntPtr.Zero, 999);

    /// <summary>The Disk column is fed by <c>LinuxProcessSnapshotProvider</c> straight from
    /// <c>/proc/[pid]/io</c>, which does not take this seam — so reporting nothing here costs the tab
    /// nothing. Pinned so it is not later mistaken for an unfinished arm.</summary>
    [Fact]
    public void TryGetIoBytes_ReportsNothing() {
        using var self = Process.GetCurrentProcess();

        Assert.False(Interop().TryGetIoBytes(self, out var bytes));
        Assert.Equal(0ul, bytes);
    }

    /// <summary>Portable managed code over the <c>/proc</c> seam, so it constructs anywhere; off Linux
    /// the links simply are not there.</summary>
    [Fact]
    public void LinuxReader_ConstructsAndDegradesOnAnyHost() =>
        Assert.Null(new LinuxProcessInterop().RevealTarget(-1));
}
