using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcPidName"/>, the name derivation the Processes and Network tabs share so
/// they cannot disagree about what a process is called.</summary>
public class ProcPidNameTests {
    /// <summary><c>cmdline</c> is NUL-separated, so argv[0] ends at the first NUL. Splitting on spaces would
    /// mangle any path containing one.</summary>
    [Theory]
    [InlineData("/usr/lib/firefox/firefox\0-contentproc\0", "comm", "firefox")]
    [InlineData("/opt/My App/run\0--flag\0", "comm", "run")]
    [InlineData("nautilus\0", "comm", "nautilus")]
    [InlineData("", "kworker/3:1H", "kworker/3:1H")]       // kernel thread: no cmdline at all
    [InlineData(null, "gnome-shell", "gnome-shell")]
    public void From_PrefersTheCommandLineBasename(string? cmdline, string comm, string expected) =>
        Assert.Equal(expected, ProcPidName.From(cmdline, comm));

    /// <summary>A process that writes a title over its own arguments leaves one space-joined string padded
    /// with NULs, not a path. Its last '/' segment is junk ("0" from sshd's "pts/0"), so the name falls back
    /// to <c>comm</c>.</summary>
    [Theory]
    [InlineData("sshd: sophia@pts/0\0\0\0\0", "sshd", "sshd")]
    [InlineData("sshd-session: sophia [priv]\0\0", "sshd-session", "sshd-session")]
    [InlineData("postgres: checkpointer \0\0", "postgres", "postgres")]
    [InlineData(
        "/opt/google/chrome/chrome --type=renderer --crashpad-handler-pid=4211"
        + " --user-data-dir=/home/sophia/chrome-profile\0\0", "chrome", "chrome")]
    public void From_RewrittenTitle_FallsBackToComm(string cmdline, string comm, string expected) =>
        Assert.Equal(expected, ProcPidName.From(cmdline, comm));

    /// <summary>A space in argv[0] is not on its own a title: a path can hold one, with or without arguments
    /// after it, and a 15-character <c>comm</c> is a truncated prefix of the real name.</summary>
    [Theory]
    [InlineData("/opt/My App/run\0", "run", "run")]
    [InlineData("/opt/My App/run\0--flag\0", "worker", "run")]
    [InlineData("/opt/Some Vendor/systemd-resolved\0", "systemd-resolve", "systemd-resolved")]
    [InlineData("/opt/My App/run\0", "", "run")]                    // no comm to check against
    public void From_SpaceInAPath_KeepsTheBasename(string cmdline, string comm, string expected) =>
        Assert.Equal(expected, ProcPidName.From(cmdline, comm));

    /// <summary>End to end through the files: the title in <c>cmdline</c>, the kernel's name in <c>comm</c>.</summary>
    [Fact]
    public void Read_RewrittenTitle_FallsBackToComm() {
        var proc = new FakeProcFileSystem()
            .WithFile("/proc/2210/cmdline", "sshd: sophia@pts/0\0\0\0")
            .WithFile("/proc/2210/comm", "sshd\n");

        Assert.Equal("sshd", ProcPidName.Read(proc, 2210));
    }

    /// <summary>Neither source names it: "" rather than a placeholder, because the two tabs' placeholders
    /// differ — Processes wants "Unknown", Network wants "PID 1234", and only the caller knows which.</summary>
    [Fact]
    public void From_NoSource_YieldsEmptyRatherThanAPlaceholder() =>
        Assert.Equal("", ProcPidName.From("", ""));

    /// <summary>The reason cmdline is preferred: the kernel truncates <c>comm</c> at 15 characters, so a
    /// name that fits nowhere else would read as <c>systemd-resolve</c> — a real daemon's name, minus its
    /// last letter, which is exactly the kind of wrong that looks right.</summary>
    [Fact]
    public void Read_PrefersCmdlineOverTheTruncatedComm() {
        var proc = new FakeProcFileSystem()
            .WithFile("/proc/812/cmdline", "/usr/lib/systemd/systemd-resolved\0")
            .WithFile("/proc/812/comm", "systemd-resolve\n");

        Assert.Equal("systemd-resolved", ProcPidName.Read(proc, 812));
    }

    /// <summary>A kernel thread has no cmdline at all, so <c>comm</c> is the only source — and it is read
    /// as a file here rather than taken from a parsed <c>stat</c>.</summary>
    [Fact]
    public void Read_EmptyCmdline_FallsBackToComm() {
        var proc = new FakeProcFileSystem()
            .WithFile("/proc/94/cmdline", "")
            .WithFile("/proc/94/comm", "kworker/2:1H\n");

        Assert.Equal("kworker/2:1H", ProcPidName.Read(proc, 94));
    }

    /// <summary>A process that exited between being listed and being read denies both files, which reads as
    /// no name rather than as an error.</summary>
    [Fact]
    public void Read_MissingProcess_YieldsEmpty() =>
        Assert.Equal("", ProcPidName.Read(new FakeProcFileSystem(), 4321));

    /// <summary>The classifier's kernel-thread input, sharing the same argv[0] split.</summary>
    [Theory]
    [InlineData("/usr/bin/gnome-shell\0", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasCommandLine_Cases(string? cmdline, bool expected) =>
        Assert.Equal(expected, ProcPidName.HasCommandLine(cmdline));
}
