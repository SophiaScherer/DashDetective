using DashDetective.Services.Diagnostics;
using DashDetective.Services.Platform.Linux;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DashDetective.Tabs.Dashboard;

/// <summary>
/// Reads static machine identity from the Linux pseudo-filesystems: the distro name from
/// <c>/etc/os-release</c>, the kernel release from <c>/proc/sys/kernel/osrelease</c>, firmware and board
/// identity from <c>/sys/class/dmi/id</c>, and the host name from the runtime. The Windows arm's WMI
/// queries are slow enough to need a background thread; these are a handful of tiny reads, but the read
/// still runs on one so both arms honour <see cref="ISystemInfoProvider"/>'s async contract identically.
///
/// Each section falls back independently, so an unpopulated DMI table — the normal case in a VM and on
/// most ARM boards — costs only the BIOS and Motherboard rows. Stateless and never throws: no caching,
/// even though <c>/etc/os-release</c> cannot change while the app runs, because <c>HardwareProviders</c>
/// requires it.
/// </summary>
internal sealed class LinuxSystemInfoProvider : ISystemInfoProvider {
    // Concatenated forward-slash literals, never Path.Combine — see IProcFileSystem.
    private const string OsReleasePath = "/etc/os-release";
    private const string KernelReleasePath = "/proc/sys/kernel/osrelease";

    private readonly IProcFileSystem _proc;

    public LinuxSystemInfoProvider() : this(new ProcFileSystem()) { }

    /// <summary>Test seam: injects the filesystem so each source — and each source's absence — can be
    /// exercised against canned fixtures from any dev machine.</summary>
    internal LinuxSystemInfoProvider(IProcFileSystem proc) => _proc = proc;

    public Task<SystemStaticInfo> GetAsync() => Task.Run(Read);

    private SystemStaticInfo Read() {
        try {
            var dmi = new DmiIdReader(_proc);

            return new SystemStaticInfo(
                ReadOs(), Environment.MachineName, ReadBios(dmi), ReadKernel(), ReadMotherboard(dmi));
        } catch (Exception e) {
            Log.Warn("SystemInfoProvider read failed", e);
            return SystemStaticInfo.Unknown;
        }
    }

    /// <summary>The distro's own display name, e.g. "Ubuntu 24.04.1 LTS". <c>PRETTY_NAME</c> is the field
    /// distros maintain for exactly this purpose; <c>NAME</c> + <c>VERSION_ID</c> reconstructs it on the
    /// few that omit it, and <see cref="RuntimeInformation.OSDescription"/> is the last resort on a host
    /// with no <c>/etc/os-release</c> at all.</summary>
    private string ReadOs() {
        var fields = OsReleaseParser.Parse(_proc.ReadAllLines(OsReleasePath));

        var pretty = OsReleaseParser.Value(fields, "PRETTY_NAME");
        if (!string.IsNullOrWhiteSpace(pretty))
            return pretty;

        var composed = DmiIdReader.Join(
            OsReleaseParser.Value(fields, "NAME"), OsReleaseParser.Value(fields, "VERSION_ID"));
        if (!string.IsNullOrWhiteSpace(composed))
            return composed;

        var description = RuntimeInformation.OSDescription;
        return string.IsNullOrWhiteSpace(description) ? "Unknown OS" : description.Trim();
    }

    /// <summary>Firmware vendor and version, e.g. "innotek GmbH VirtualBox".</summary>
    private static string ReadBios(DmiIdReader dmi) {
        var text = DmiIdReader.Join(dmi.BiosVendor, dmi.BiosVersion);
        return string.IsNullOrWhiteSpace(text) ? "Unknown BIOS" : text;
    }

    /// <summary>The running kernel release, e.g. "6.8.0-51-generic" — the closest analogue to the Windows
    /// build number this row carries, and what <c>uname -r</c> prints.</summary>
    private string ReadKernel() {
        var release = _proc.ReadAllText(KernelReleasePath)?.Trim();
        return string.IsNullOrWhiteSpace(release) ? "Unknown" : release;
    }

    /// <summary>Board vendor and product, e.g. "Oracle Corporation VirtualBox". Falls back to the system
    /// (chassis) fields, which laptops and VMs populate more reliably than the board ones.</summary>
    private static string ReadMotherboard(DmiIdReader dmi) {
        var board = DmiIdReader.Join(dmi.BoardVendor, dmi.BoardName);
        if (!string.IsNullOrWhiteSpace(board))
            return board;

        var system = DmiIdReader.Join(dmi.SysVendor, dmi.ProductName);
        return string.IsNullOrWhiteSpace(system) ? "Unknown motherboard" : system;
    }
}
