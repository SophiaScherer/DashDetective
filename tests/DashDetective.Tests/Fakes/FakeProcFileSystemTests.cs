using Xunit;

namespace DashDetective.Tests.Fakes;

/// <summary>Covers <see cref="FakeProcFileSystem"/> itself: it has to honour the same never-throw,
/// empty-on-miss contract as <c>ProcFileSystem</c>, or every Linux provider test built on it is testing
/// the wrong thing.</summary>
public class FakeProcFileSystemTests {
    [Fact]
    public void ReadAllText_StagedFile_ReturnsBody() {
        var fs = new FakeProcFileSystem().WithFile("/proc/stat", "cpu 1 2 3");

        Assert.Equal("cpu 1 2 3", fs.ReadAllText("/proc/stat"));
    }

    [Fact]
    public void ReadAllText_MissingFile_ReturnsNull() {
        Assert.Null(new FakeProcFileSystem().ReadAllText("/proc/stat"));
    }

    [Fact]
    public void ReadAllLines_MissingFile_ReturnsEmpty() {
        Assert.Empty(new FakeProcFileSystem().ReadAllLines("/proc/stat"));
    }

    [Fact]
    public void ReadAllLines_SplitsOnNewlines() {
        var fs = new FakeProcFileSystem().WithFile("/proc/stat", "cpu 1\ncpu0 2\ncpu1 3");

        Assert.Equal(["cpu 1", "cpu0 2", "cpu1 3"], fs.ReadAllLines("/proc/stat"));
    }

    /// <summary>A pseudo-file ends in a newline; <c>File.ReadAllLines</c> yields no trailing empty entry
    /// for it, so a provider that indexes the last line must see the same here.</summary>
    [Fact]
    public void ReadAllLines_TrailingNewline_YieldsNoEmptyEntry() {
        var fs = new FakeProcFileSystem().WithFile("/proc/meminfo", "MemTotal: 1 kB\nMemFree: 2 kB\n");

        Assert.Equal(["MemTotal: 1 kB", "MemFree: 2 kB"], fs.ReadAllLines("/proc/meminfo"));
    }

    [Fact]
    public void ReadAllLines_CarriageReturns_SplitLikeNewlines() {
        var fs = new FakeProcFileSystem().WithFile("/proc/stat", "cpu 1\r\ncpu0 2");

        Assert.Equal(["cpu 1", "cpu0 2"], fs.ReadAllLines("/proc/stat"));
    }

    /// <summary>The point of deriving listings: staging a leaf file is what makes its ancestors
    /// enumerable, so a fixture tree cannot be internally inconsistent.</summary>
    [Fact]
    public void ListDirectory_DerivesChildrenFromStagedPaths() {
        var fs = new FakeProcFileSystem()
            .WithFile("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq", "3600000")
            .WithFile("/sys/devices/system/cpu/cpu1/cpufreq/scaling_cur_freq", "3600000")
            .WithFile("/sys/devices/system/cpu/online", "0-1");

        Assert.Equal(["cpu0", "cpu1", "online"], fs.ListDirectory("/sys/devices/system/cpu"));
    }

    /// <summary>A directory listing reports entry names, never full paths — the contract the real one
    /// gets from <c>Path.GetFileName</c>.</summary>
    [Fact]
    public void ListDirectory_TrailingSlash_ListsTheSameEntries() {
        var fs = new FakeProcFileSystem().WithFile("/proc/1/stat", "1 (init) S");

        Assert.Equal(["1"], fs.ListDirectory("/proc"));
        Assert.Equal(["stat"], fs.ListDirectory("/proc/1/"));
    }

    [Fact]
    public void ListDirectory_MissingDirectory_ReturnsEmpty() {
        Assert.Empty(new FakeProcFileSystem().ListDirectory("/sys/class/hwmon"));
    }

    [Fact]
    public void Exists_CoversFilesLinksAndDerivedDirectories() {
        var fs = new FakeProcFileSystem()
            .WithFile("/proc/stat", "cpu 1")
            .WithLink("/sys/block/sda/device", "/sys/devices/pci0000:00/host0/target0/0:0:0:0");

        Assert.True(fs.Exists("/proc/stat"));
        Assert.True(fs.Exists("/proc"));                       // derived from the staged file
        Assert.True(fs.Exists("/sys/block/sda/device"));       // staged as a link
        Assert.True(fs.Exists("/sys/block"));                  // derived from the staged link
        Assert.False(fs.Exists("/proc/meminfo"));
    }

    [Fact]
    public void ResolveLink_StagedAndMissing() {
        var fs = new FakeProcFileSystem().WithLink("/proc/self/fd/3", "/var/log/syslog");

        Assert.Equal("/var/log/syslog", fs.ResolveLink("/proc/self/fd/3"));
        Assert.Null(fs.ResolveLink("/proc/self/fd/4"));
    }

    /// <summary>Reads are recorded so a provider test can pin which source it actually consulted — the
    /// fallback chains in the frequency sampler depend on that being observable.</summary>
    [Fact]
    public void Reads_RecordsEveryPathInOrder() {
        var fs = new FakeProcFileSystem().WithFile("/proc/stat", "cpu 1");

        fs.ReadAllLines("/proc/stat");
        fs.ReadAllText("/proc/cpuinfo");

        Assert.Equal(["/proc/stat", "/proc/cpuinfo"], fs.Reads);
    }
}
