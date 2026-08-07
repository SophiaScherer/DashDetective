using DashDetective.Tabs.Dashboard;
using DashDetective.Tests.Fakes;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Dashboard;

/// <summary>Covers <see cref="LinuxSystemInfoProvider"/>: the four sources it assembles, the independent
/// per-row degradation that keeps one dead source from blanking the panel, and the fallback chains that
/// decide what a VM and a bare-metal box each show.</summary>
public class LinuxSystemInfoProviderTests {
    private const string OsReleasePath = "/etc/os-release";
    private const string KernelPath = "/proc/sys/kernel/osrelease";

    /// <summary>A fully-staged host: an Ubuntu <c>os-release</c>, a kernel release, and a VirtualBox DMI
    /// tree — the exact combination the VM acceptance check expects to see on screen.</summary>
    private static FakeProcFileSystem FullTree() =>
        new FakeProcFileSystem()
            .WithFile(OsReleasePath, ProcFixtures.OsRelease)
            .WithFile(KernelPath, "6.8.0-51-generic\n")
            .WithVirtualBoxDmi();

    private static Task<SystemStaticInfo> Read(FakeProcFileSystem proc) =>
        new LinuxSystemInfoProvider(proc).GetAsync();

    [Fact]
    public async Task GetAsync_ReportsThePrettyNameAsTheOs() =>
        Assert.Equal("Ubuntu 24.04.1 LTS", (await Read(FullTree())).Os);

    /// <summary>The kernel release is what <c>uname -r</c> prints, and the closest analogue to the Windows
    /// build number this row carries.</summary>
    [Fact]
    public async Task GetAsync_ReportsTheKernelReleaseAsTheBuild() =>
        Assert.Equal("6.8.0-51-generic", (await Read(FullTree())).Build);

    [Fact]
    public async Task GetAsync_ComposesBiosAndMotherboardFromDmi() {
        var info = await Read(FullTree());

        Assert.Equal("innotek GmbH VirtualBox", info.Bios);
        Assert.Equal("Oracle Corporation VirtualBox", info.Motherboard);
    }

    /// <summary>The host name comes from the runtime on every platform, so it is the one row that cannot
    /// degrade.</summary>
    [Fact]
    public async Task GetAsync_ReportsTheMachineNameAsTheDevice() =>
        Assert.Equal(Environment.MachineName, (await Read(FullTree())).Device);

    /// <summary>A distro that omits <c>PRETTY_NAME</c> still gets a usable label rather than falling all
    /// the way through to the kernel's verbose description.</summary>
    [Fact]
    public async Task GetAsync_WithNoPrettyName_ComposesNameAndVersionId() {
        var proc = new FakeProcFileSystem()
            .WithFile(OsReleasePath, "NAME=\"Debian GNU/Linux\"\nVERSION_ID=12\n");

        Assert.Equal("Debian GNU/Linux 12", (await Read(proc)).Os);
    }

    /// <summary>With no <c>/etc/os-release</c> at all the runtime's description is the last resort — it is
    /// verbose, but it is real, and it beats reporting nothing.</summary>
    [Fact]
    public async Task GetAsync_WithNoOsRelease_FallsBackToTheRuntimeDescription() {
        var os = (await Read(new FakeProcFileSystem())).Os;

        Assert.NotEqual("Unknown OS", os);
        Assert.NotEmpty(os);
    }

    /// <summary>
    /// The degradation that matters most: <c>/sys/class/dmi/id/board_serial</c> and friends are mode 0400,
    /// so a normal user's read of them fails. Nothing else may fail with them — the board string is built
    /// only from world-readable files, and the fixture stages exactly those.
    /// </summary>
    [Fact]
    public async Task GetAsync_NeverReadsTheRootOnlyDmiFiles() {
        var proc = FullTree();

        var info = await Read(proc);

        Assert.Equal("Oracle Corporation VirtualBox", info.Motherboard);
        Assert.DoesNotContain(proc.Reads, path =>
            path.EndsWith("serial", StringComparison.Ordinal)
            || path.EndsWith("uuid", StringComparison.Ordinal));
    }

    /// <summary>An unpopulated DMI table costs the two DMI rows and nothing else — the OS and kernel rows
    /// come from different files and must survive.</summary>
    [Fact]
    public async Task GetAsync_WithNoDmiTable_DegradesOnlyTheDmiRows() {
        var proc = new FakeProcFileSystem()
            .WithFile(OsReleasePath, ProcFixtures.OsRelease)
            .WithFile(KernelPath, "6.8.0-51-generic\n");

        var info = await Read(proc);

        Assert.Equal("Unknown BIOS", info.Bios);
        Assert.Equal("Unknown motherboard", info.Motherboard);
        Assert.Equal("Ubuntu 24.04.1 LTS", info.Os);
        Assert.Equal("6.8.0-51-generic", info.Build);
    }

    /// <summary>Laptops and VMs often leave the board fields blank but populate the chassis ones.</summary>
    [Fact]
    public async Task GetAsync_WithNoBoardFields_FallsBackToTheSystemVendorAndProduct() {
        var proc = new FakeProcFileSystem()
            .WithFile("/sys/class/dmi/id/sys_vendor", "LENOVO\n")
            .WithFile("/sys/class/dmi/id/product_name", "ThinkPad X1 Carbon\n");

        Assert.Equal("LENOVO ThinkPad X1 Carbon", (await Read(proc)).Motherboard);
    }

    /// <summary>An entirely unreadable <c>/proc</c> is a snapshot, not an exception — every row carries its
    /// own placeholder and the panel renders.</summary>
    [Fact]
    public async Task GetAsync_WithNothingReadable_ReportsPlaceholdersRatherThanThrowing() {
        var info = await Read(new FakeProcFileSystem());

        Assert.Equal("Unknown BIOS", info.Bios);
        Assert.Equal("Unknown", info.Build);
        Assert.Equal("Unknown motherboard", info.Motherboard);
    }
}
