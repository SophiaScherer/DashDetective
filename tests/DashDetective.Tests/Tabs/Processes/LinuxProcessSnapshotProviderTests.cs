using DashDetective.Services.Platform.Linux;
using DashDetective.Tabs.Processes;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Covers <see cref="LinuxProcessSnapshotProvider"/>: the fields it assembles per process, the
/// interval math, the race that skips a vanished PID, and the reader this platform resolves to.</summary>
public class LinuxProcessSnapshotProviderTests {
    /// <summary>Stages one process's five files. <paramref name="io"/> null stands for the 0400 denial on a
    /// process you do not own.</summary>
    private static void StageProcess(
        FakeProcFileSystem proc, int pid, string stat, string status, string cmdline, string cgroup,
        string? io = null) {
        var root = "/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/";
        proc.WithFile(root + "stat", stat)
            .WithFile(root + "status", status)
            .WithFile(root + "cmdline", cmdline)
            .WithFile(root + "cgroup", cgroup);

        if (io is not null)
            proc.WithFile(root + "io", io);
    }

    /// <summary>A desktop: GNOME Shell, a Firefox content process with a hostile name, and a kernel
    /// thread — one of each group.</summary>
    private static FakeProcFileSystem Desktop() {
        var proc = new FakeProcFileSystem();
        StageProcess(proc, 412, ProcFixtures.ProcPidStat, ProcFixtures.ProcPidStatus,
            "/usr/bin/gnome-shell\0", ProcFixtures.ProcCgroupApp, ProcFixtures.ProcPidIo);
        StageProcess(proc, 1337, ProcFixtures.ProcPidStatHostileName, ProcFixtures.ProcPidStatus,
            "/usr/lib/firefox/firefox\0-contentproc\0-childID\0 2\0", ProcFixtures.ProcCgroupHybrid);
        StageProcess(proc, 58, ProcFixtures.ProcPidStatKernelThread,
            ProcFixtures.ProcPidStatusKernelThread, "", "");
        return proc;
    }

    private static async Task<IReadOnlyList<ProcessInfo>> SnapshotAsync(IProcFileSystem proc) =>
        await new LinuxProcessSnapshotProvider(proc).GetAsync();

    private static ProcessInfo Find(IReadOnlyList<ProcessInfo> processes, int pid) =>
        processes.Single(process => process.Pid == pid);

    [Fact]
    public async Task GetAsync_ListsEveryProcessUnderProc() {
        var processes = await SnapshotAsync(Desktop());

        Assert.Equal([58, 412, 1337], processes.Select(process => process.Pid).Order());
    }

    [Fact]
    public async Task GetAsync_AssemblesTheRowFromAllFiveFiles() {
        var shell = Find(await SnapshotAsync(Desktop()), 412);

        Assert.Equal("gnome-shell", shell.Name);
        Assert.Equal(1, shell.ParentPid);
        Assert.Equal(14, shell.ThreadCount);
        Assert.Equal(345678L * 1024, shell.MemoryBytes);
        Assert.Equal("Running", shell.Status);
        Assert.Equal(ProcessCategory.App, shell.Category);
    }

    /// <summary>The name is <c>cmdline</c>'s basename, not <c>comm</c> — <c>comm</c> truncates at 15
    /// characters — and carries no <c>.exe</c>, which is the Windows provider's suffix alone.</summary>
    [Fact]
    public async Task GetAsync_NamesFromTheCommandLineBasenameWithoutAnExeSuffix() {
        var firefox = Find(await SnapshotAsync(Desktop()), 1337);

        Assert.Equal("firefox", firefox.Name);
        Assert.DoesNotContain(".exe", firefox.Name, StringComparison.Ordinal);
    }

    /// <summary>A kernel thread has no <c>cmdline</c> at all, so the name falls back to <c>comm</c> — and
    /// <c>comm</c> is the one place its name exists.</summary>
    [Fact]
    public async Task GetAsync_KernelThread_FallsBackToCommAndReadsAsSystem() {
        var worker = Find(await SnapshotAsync(Desktop()), 58);

        Assert.Equal("kworker/3:1H-events_highpri", worker.Name);
        Assert.Equal(ProcessCategory.Windows, worker.Category);
        Assert.Equal(0, worker.MemoryBytes);
    }

    /// <summary>Per-process GPU has no rootless Linux source at all — a permanent gap, so the column is
    /// always 0 rather than sometimes populated.</summary>
    [Fact]
    public async Task GetAsync_NeverReportsGpuUsage() =>
        Assert.All(await SnapshotAsync(Desktop()), process => Assert.Equal(0, process.GpuPercent));

    /// <summary>The #1 source of flakiness: <c>/proc/[pid]</c> vanishes mid-walk constantly. The PID is
    /// skipped and every other process still lists.</summary>
    [Fact]
    public async Task GetAsync_ProcessExitedMidWalk_SkipsItAndKeepsTheRest() {
        var proc = Desktop();
        StageProcess(proc, 999, "", "", "", ""); // directory present, stat already gone

        var processes = await SnapshotAsync(proc);

        Assert.Equal([58, 412, 1337], processes.Select(process => process.Pid).Order());
    }

    /// <summary>Rates need two points, so the first pass reports none — the same contract the Windows
    /// provider has.</summary>
    [Fact]
    public async Task GetAsync_FirstSnapshot_ReportsNoRates() {
        var shell = Find(await SnapshotAsync(Desktop()), 412);

        Assert.Equal(0, shell.CpuPercent);
        Assert.Equal(0, shell.DiskBytesPerSec);
    }

    /// <summary>The second pass diffs against the first. The exact figures depend on the elapsed wall clock,
    /// so this pins that a burning process is reported as busy at all; <see cref="ComputeCpuPercent_Cases"/>
    /// pins the arithmetic.</summary>
    [Fact]
    public async Task GetAsync_SecondSnapshot_ReportsRatesForAProcessThatMoved() {
        var proc = Desktop();
        var provider = new LinuxProcessSnapshotProvider(proc);
        _ = await provider.GetAsync();

        // Same process, later counters: +600 ticks of CPU and +1 MiB through the syscall layer.
        StageProcess(proc, 412,
            ProcFixtures.ProcPidStat.Replace(" 1200 340 ", " 1800 340 ", StringComparison.Ordinal),
            ProcFixtures.ProcPidStatus, "/usr/bin/gnome-shell\0", ProcFixtures.ProcCgroupApp,
            "rchar: 4194304\nwchar: 1048576\n");

        var shell = Find(await provider.GetAsync(), 412);

        Assert.True(shell.CpuPercent > 0, "a process that burned ticks must report CPU");
        Assert.True(shell.DiskBytesPerSec > 0, "a process that moved bytes must report a disk rate");
    }

    /// <summary>A process you do not own denies <c>io</c>, which is a blank rate rather than an error — the
    /// Firefox fixture stages no <c>io</c> file for exactly this.</summary>
    [Fact]
    public async Task GetAsync_DeniedIo_LeavesTheDiskRateAtZero() =>
        Assert.Equal(0, Find(await SnapshotAsync(Desktop()), 1337).DiskBytesPerSec);

    /// <summary>Nothing readable — a non-Linux host reached through the seam, or a container without
    /// <c>/proc</c> — is an empty list, never a throw.</summary>
    [Fact]
    public async Task GetAsync_NoProc_IsEmpty() =>
        Assert.Empty(await SnapshotAsync(new FakeProcFileSystem()));

    // ----- The pure helpers -----

    /// <summary>Ticks are USER_HZ (100/sec) and the result is normalised by core count: 200 ticks is 2 CPU
    /// seconds, which over 1 second on 4 cores is 50%.</summary>
    [Theory]
    [InlineData(200UL, 1.0, 4, 50)]
    [InlineData(100UL, 1.0, 1, 100)]
    [InlineData(50UL, 2.0, 1, 25)]
    [InlineData(0UL, 1.0, 4, 0)]
    [InlineData(200UL, 0, 4, 0)]        // no interval yet
    [InlineData(100000UL, 0.001, 1, 100)] // clamped, never over 100
    public void ComputeCpuPercent_Cases(ulong ticks, double seconds, int processors, double expected) =>
        Assert.Equal(expected, LinuxProcessSnapshotProvider.ComputeCpuPercent(ticks, seconds, processors), 6);

    /// <summary>The derivation itself now lives in <c>ProcPidName</c>, shared with the Network tab; what is
    /// this tab's own is the placeholder for a process that names itself nowhere.</summary>
    [Theory]
    [InlineData("/usr/lib/firefox/firefox\0-contentproc\0", "comm", "firefox")]
    [InlineData("", "kworker/3:1H", "kworker/3:1H")]       // kernel thread: no cmdline at all
    [InlineData("", "", "Unknown")]
    public void NameFrom_FallsBackToThisTabsPlaceholder(string? cmdline, string comm, string expected) =>
        Assert.Equal(expected, LinuxProcessSnapshotProvider.NameFrom(cmdline, comm));

    /// <summary>Windows reports only "Running" and "Not responding", so states meaning the same thing there
    /// collapse to "Running" — which is also the exact string that tints the row's dot green.</summary>
    [Theory]
    [InlineData('R', "Running")]
    [InlineData('S', "Running")]
    [InlineData('I', "Running")]
    [InlineData('X', "Running")]
    [InlineData('D', "Waiting")]
    [InlineData('T', "Suspended")]
    [InlineData('t', "Suspended")]
    [InlineData('Z', "Zombie")]
    public void StatusFor_MapsTheStateChar(char state, string expected) =>
        Assert.Equal(expected, LinuxProcessSnapshotProvider.StatusFor(state));

    // ----- Reader identity -----

    /// <summary>The arm a green Windows run never executes. Grep the suite for
    /// <c>OperatingSystem.IsLinux</c> after touching any factory — this is why.</summary>
    [Fact]
    public void ForCurrentPlatform_PicksThisPlatformsReader() {
        var provider = IProcessSnapshotProvider.ForCurrentPlatform(IProcessInterop.ForCurrentPlatform());

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsProcessSnapshotProvider>(provider);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxProcessSnapshotProvider>(provider);
        else
            Assert.IsType<UnsupportedProcessSnapshotProvider>(provider);
    }

    /// <summary>The no-data arm honours the same contract, since it is what a platform with no milestone
    /// actually gets.</summary>
    [Fact]
    public async Task UnsupportedProvider_ReportsNoProcesses() =>
        Assert.Empty(await new UnsupportedProcessSnapshotProvider().GetAsync());
}
