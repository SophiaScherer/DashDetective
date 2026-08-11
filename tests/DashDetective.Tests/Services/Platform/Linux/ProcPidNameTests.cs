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
