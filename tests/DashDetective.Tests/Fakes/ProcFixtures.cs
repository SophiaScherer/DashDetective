using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Canned <c>/proc</c> and <c>/sys</c> file bodies shared by the Linux provider tests, as plain C# raw
/// string literals — no embedded resources and no new build actions, matching the codebase's
/// zero-dependency ethos. A fixture used by exactly one test stays inline in that test.
/// </summary>
internal static class ProcFixtures {
    /// <summary>
    /// A modern <c>/proc/stat</c>: the aggregate plus four cores, all ten columns, and the non-<c>cpu</c>
    /// trailer that a parser has to skip. Every line totals to 17% busy — 10000 jiffies across
    /// <c>user…steal</c> of which 8300 are <c>idle + iowait</c> — so a diff against a doubled snapshot
    /// lands on a round number.
    /// </summary>
    public const string ProcStat =
        """
        cpu  1000 100 500 8000 300 0 100 0 0 0
        cpu0 250 25 125 2000 75 0 25 0 0 0
        cpu1 250 25 125 2000 75 0 25 0 0 0
        cpu2 250 25 125 2000 75 0 25 0 0 0
        cpu3 250 25 125 2000 75 0 25 0 0 0
        intr 45678901 22 1234 0 0 0 0 0 0 1
        ctxt 98765432
        btime 1717171717
        processes 123456
        procs_running 2
        procs_blocked 0
        softirq 12345678 1 234567 89 12345 0 0 6789 123456 0 987654
        """;

    /// <summary>The pre-2.6.11 seven-column form (no <c>steal</c>, <c>guest</c> or <c>guest_nice</c>) —
    /// what proves a parser reads by index with a length check rather than assuming ten.</summary>
    public const string ProcStatLegacy =
        """
        cpu  1000 100 500 8000 300 0 100
        cpu0 500 50 250 4000 150 0 50
        cpu1 500 50 250 4000 150 0 50
        """;

    /// <summary>
    /// A two-core <c>/proc/cpuinfo</c>. Built by joining escaped strings rather than as a raw literal
    /// because the real file separates key from value with <b>tabs</b>, and the repo's
    /// <c>indent_style = space</c> makes a literal tab inside a raw literal a formatting hazard. The tabs
    /// are the point: a parser that splits on a fixed layout instead of trimming around the colon passes
    /// a space-separated fixture and fails on a real machine.
    /// </summary>
    public static readonly string ProcCpuInfo = string.Join('\n', [
        "processor\t: 0",
        "vendor_id\t: GenuineIntel",
        "model name\t: Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz",
        "cpu MHz\t\t: 3600.000",
        "cache size\t: 12288 KB",
        "",
        "processor\t: 1",
        "vendor_id\t: GenuineIntel",
        "model name\t: Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz",
        "cpu MHz\t\t: 2400.000",
        "cache size\t: 12288 KB",
        ""]);

    /// <summary>
    /// A stock Ubuntu <c>/proc/meminfo</c>, trimmed to the fields the app reads plus enough neighbours to
    /// prove the parser skips what it does not know. 16 GiB total; the numbers are round in <b>kB</b> so a
    /// byte expectation is a visible ×1024. <c>HugePages_Total</c> is deliberately present: it is a count
    /// with no unit, which a parser that assumes every value is kB gets wrong.
    /// </summary>
    public const string ProcMeminfo =
        """
        MemTotal:       16777216 kB
        MemFree:         2097152 kB
        MemAvailable:    8388608 kB
        Buffers:          524288 kB
        Cached:          5242880 kB
        SwapCached:            0 kB
        Active:          6291456 kB
        Inactive:        4194304 kB
        SwapTotal:       2097152 kB
        SwapFree:        2097152 kB
        Dirty:              1024 kB
        Slab:            1048576 kB
        SReclaimable:     786432 kB
        SUnreclaim:       262144 kB
        CommitLimit:    10485760 kB
        Committed_AS:    9437184 kB
        HugePages_Total:       0
        Hugepagesize:       2048 kB
        """;

    /// <summary>
    /// A four-core, eight-thread AMD <c>/proc/cpuinfo</c> across <b>two sockets</b> — the shape
    /// <see cref="ProcCpuInfo"/> cannot exercise. Blocks 0–3 are package 0 and blocks 4–7 package 1, and
    /// each package's two cores appear twice, so the distinct <c>(physical id, core id)</c> pairs total 4
    /// while the blocks total 8. Counting blocks, or trusting <c>cpu cores</c> alone, gets a different
    /// answer for each — which is the point. The model name carries no "@ clock" suffix, as AMD's do not.
    /// </summary>
    public static readonly string AmdCpuInfo = string.Join('\n', BuildAmdBlocks());

    /// <summary>A <c>/proc/loadavg</c>: three load averages, then <c>nr_running/nr_threads</c> — 2 runnable
    /// of <b>1234 threads</b>, not processes — then the last-used PID.</summary>
    public const string ProcLoadavg = "0.52 0.58 0.59 2/1234 56789\n";

    /// <summary>
    /// A stock <c>/etc/os-release</c>. <c>PRETTY_NAME</c> is quoted and <c>VERSION_ID</c> is not, in the
    /// same body: both forms are legal shell and a parser that handles only one silently mangles the
    /// other. The comment line and the <c>=</c> inside <c>HOME_URL</c> are the malformed-input cases.
    /// </summary>
    public const string OsRelease =
        """
        # a comment the parser must skip
        PRETTY_NAME="Ubuntu 24.04.1 LTS"
        NAME="Ubuntu"
        VERSION_ID=24.04
        VERSION="24.04.1 LTS (Noble Numbat)"
        ID=ubuntu
        ID_LIKE=debian
        HOME_URL="https://www.ubuntu.com/?q=1"
        """;

    /// <summary>Stages a VirtualBox guest's <c>/sys/class/dmi/id</c> tree onto a fake filesystem — the
    /// values the VM acceptance check expects to see on screen. The root-only <c>board_serial</c>,
    /// <c>product_serial</c> and <c>product_uuid</c> are deliberately absent, which is what a non-root
    /// read of them looks like.</summary>
    public static FakeProcFileSystem WithVirtualBoxDmi(this FakeProcFileSystem proc) =>
        proc.WithFile("/sys/class/dmi/id/board_vendor", "Oracle Corporation\n")
            .WithFile("/sys/class/dmi/id/board_name", "VirtualBox\n")
            .WithFile("/sys/class/dmi/id/board_version", "1.2\n")
            .WithFile("/sys/class/dmi/id/bios_vendor", "innotek GmbH\n")
            .WithFile("/sys/class/dmi/id/bios_version", "VirtualBox\n")
            .WithFile("/sys/class/dmi/id/bios_date", "12/01/2006\n")
            .WithFile("/sys/class/dmi/id/sys_vendor", "innotek GmbH\n")
            .WithFile("/sys/class/dmi/id/product_name", "VirtualBox\n");

    /// <summary>
    /// A stock Ubuntu GNOME <c>/proc/mounts</c>, trimmed to the shapes that matter. It carries the three
    /// traps of the real file: the pseudo-filesystem flood (<c>tmpfs</c>, <c>cgroup2</c>, <c>proc</c>), a
    /// snap mount backed by a <c>loop</c> device, and <c>/dev/sda2</c> listed twice — once as the root and
    /// again as a bind mount, which a reader that does not dedupe counts twice into the drive's capacity.
    /// The <c>\040</c> in the last mount point is the kernel's octal escape for a space.
    /// </summary>
    public const string ProcMounts =
        """
        sysfs /sys sysfs rw,nosuid,nodev,noexec,relatime 0 0
        proc /proc proc rw,nosuid,nodev,noexec,relatime 0 0
        udev /dev devtmpfs rw,nosuid,relatime,size=8110044k 0 0
        tmpfs /run tmpfs rw,nosuid,nodev,noexec,relatime,size=1629636k 0 0
        cgroup2 /sys/fs/cgroup cgroup2 rw,nosuid,nodev,noexec,relatime 0 0
        /dev/sda2 / ext4 rw,relatime,errors=remount-ro 0 0
        /dev/sda1 /boot/efi vfat rw,relatime,fmask=0077 0 0
        /dev/loop3 /snap/firefox/4793 squashfs ro,nodev,relatime 0 0
        /dev/loop7 /snap/gnome-42-2204/141 squashfs ro,nodev,relatime 0 0
        tmpfs /run/user/1000 tmpfs rw,nosuid,nodev,relatime,size=1629632k 0 0
        /dev/sda2 /var/lib/docker/btrfs ext4 rw,relatime,errors=remount-ro 0 0
        /dev/sdb1 /media/user/My\040Backup exfat rw,nosuid,nodev,relatime 0 0
        """;

    /// <summary>
    /// A <c>/proc/diskstats</c> for the same machine, in the <b>14-field</b> pre-4.18 form. Numbers are
    /// round so a rate over a one-second interval is readable: <c>sda</c> has read 2048 sectors (1 MiB) and
    /// written 4096 (2 MiB), and <c>io_ticks</c> sits at 1000 ms. <c>sda1</c> and <c>sda2</c> are listed
    /// alongside their disk, as the kernel always lists them — summing all three double-counts.
    /// </summary>
    public const string ProcDiskstats =
        """
        7 3 loop3 120 0 960 40 0 0 0 0 0 60 40
        8 0 sda 5000 100 2048 800 3000 200 4096 900 0 1000 1700
        8 1 sda1 200 0 512 30 100 0 256 40 0 60 70
        8 2 sda2 4800 100 1536 770 2900 200 3840 860 0 940 1630
        11 0 sr0 0 0 0 0 0 0 0 0 0 0 0
        """;

    /// <summary>The same <c>sda</c> row one second later: +1024 sectors read, +2048 written, +250 ms of
    /// <c>io_ticks</c> and 4 more completed transfers, so a one-second diff lands on 512 KiB/s read,
    /// 1 MiB/s written and 25% active.</summary>
    public const string ProcDiskstatsLater =
        """
        8 0 sda 5002 100 3072 850 3002 200 6144 950 2 1250 1700
        """;

    /// <summary>
    /// The <b>20-field</b> 5.5+ form — discards and flushes appended after the fourteen a parser may
    /// assume. Reading the trailing columns as if they were the leading ones is what this catches.
    /// </summary>
    public const string ProcDiskstatsModern =
        """
        259 0 nvme0n1 5000 100 2048 800 3000 200 4096 900 0 1000 1700 10 0 80 5 3 12
        """;

    /// <summary>
    /// Stages a VirtualBox guest's <c>/sys/block</c> tree: one SATA disk with two partitions, three snap
    /// <c>loop</c> devices and an optical <c>sr0</c> — the flood the Storage tab must not render. Sizes are
    /// in 512-byte sectors, so <c>sda</c>'s 41943040 is 20 GiB.
    /// </summary>
    public static FakeProcFileSystem WithVirtualBoxBlockTree(this FakeProcFileSystem proc) {
        proc.WithFile("/sys/block/sda/dev", "8:0\n")
            .WithFile("/sys/block/sda/size", "41943040\n")
            .WithFile("/sys/block/sda/removable", "0\n")
            .WithFile("/sys/block/sda/queue/rotational", "1\n")
            .WithFile("/sys/block/sda/device/model", "VBOX HARDDISK\n")
            .WithFile("/sys/block/sda/device/vendor", "ATA\n")
            .WithFile("/sys/block/sda/sda1/dev", "8:1\n")
            .WithFile("/sys/block/sda/sda1/size", "1048576\n")
            .WithFile("/sys/block/sda/sda2/dev", "8:2\n")
            .WithFile("/sys/block/sda/sda2/size", "40894464\n")
            .WithFile("/sys/block/sr0/dev", "11:0\n")
            .WithFile("/sys/block/sr0/size", "2097152\n");

        foreach (var loop in new[] { 3, 7, 11 })
            proc.WithFile($"/sys/block/loop{loop}/dev", $"7:{loop}\n")
                .WithFile($"/sys/block/loop{loop}/size", "1024\n")
                .WithFile($"/sys/block/loop{loop}/queue/rotational", "0\n");

        return proc;
    }

    /// <summary>Stages an LVM root over <see cref="WithVirtualBoxBlockTree"/>'s disk: <c>dm-0</c> backed by
    /// <c>sda2</c>, plus the <c>/dev/mapper</c> symlink a mount line names it by. Ubuntu Server's default
    /// layout, where treating <c>dm-*</c> as unshowable would drop the root volume entirely.</summary>
    public static FakeProcFileSystem WithLvmRoot(this FakeProcFileSystem proc) =>
        proc.WithFile("/sys/block/dm-0/dev", "252:0\n")
            .WithFile("/sys/block/dm-0/size", "40894464\n")
            .WithFile("/sys/block/dm-0/slaves/sda2", "")
            .WithLink("/dev/mapper/ubuntu--vg-ubuntu--lv", "/dev/dm-0");

    /// <summary>
    /// A stock <c>/proc/[pid]/stat</c>: GNOME Shell, state <c>S</c>, parent <c>1</c>, 1200 + 340 ticks of
    /// user + system time and 14 threads. The trailing fields past <c>num_threads</c> are present because a
    /// parser must reach index 17 and stop, not consume the line.
    /// </summary>
    public const string ProcPidStat =
        "412 (gnome-shell) S 1 412 412 0 -1 4194560 987654 1234 56 0 1200 340 12 3 20 0 14 0 " +
        "5678 3456789012 45678 18446744073709551615 1 1 0 0 0 0 0 4096 16781312 0 0 0 17 2 0 0 0 0 0 0 0 0 0 0 0 0 0\n";

    /// <summary>
    /// The same shape for a process whose <c>comm</c> carries <b>both</b> a space and a nested pair of
    /// parentheses — Firefox's content processes are the real-world case. A reader that splits the whole
    /// line on spaces, or stops at the <b>first</b> <c>)</c>, lands on the wrong token for every field after
    /// the name and reports a garbage parent PID for exactly the processes users care about.
    /// </summary>
    public const string ProcPidStatHostileName =
        "1337 (Web (Content) 2) S 1300 1337 1337 0 -1 4194560 22222 0 0 0 3000 500 0 0 20 0 26 0 " +
        "9999 987654321 12345 18446744073709551615 1 1 0 0 0 0 0 0 0 0 0 0 17 4 0 0 0 0 0 0 0 0 0 0 0\n";

    /// <summary>A kernel thread: <c>kthreadd</c>'s child, square-bracketed in <c>ps</c> because it has no
    /// <c>cmdline</c> at all. Parent 2 is the marker the classifier keys on.</summary>
    public const string ProcPidStatKernelThread =
        "58 (kworker/3:1H-events_highpri) I 2 0 0 0 -1 69238880 0 0 0 0 0 8 0 0 0 -20 1 0 " +
        "112 0 0 18446744073709551615 0 0 0 0 0 0 0 2147483647 0 0 0 0 17 3 0 0 0 0 0 0 0 0 0 0 0\n";

    /// <summary>One <c>/proc/stat</c> line — <c>StatLine("cpu0", 250, 25, …)</c>. Lets a test state the
    /// exact jiffy deltas it wants to assert on instead of counting columns in a literal.</summary>
    public static string StatLine(string cpu, params long[] fields) =>
        cpu + " " + string.Join(' ', fields.Select(f => f.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Assembles a <c>/proc/stat</c> body from lines built with <see cref="StatLine"/>.</summary>
    public static string Stat(params string[] lines) => string.Join('\n', lines);

    /// <summary>Builds <see cref="AmdCpuInfo"/>'s eight blocks: two sockets of two cores, each core with
    /// two hyperthread siblings. Tab-separated like the real file, and blank-line separated like the real
    /// file — including the trailing blank, which must not yield a ninth processor.</summary>
    private static List<string> BuildAmdBlocks() {
        var lines = new List<string>();
        for (var processor = 0; processor < 8; processor++) {
            lines.Add($"processor\t: {processor}");
            lines.Add("vendor_id\t: AuthenticAMD");
            lines.Add("model name\t: AMD Ryzen 5 7600X 4-Core Processor");
            lines.Add("cpu MHz\t\t: 3400.000");
            lines.Add("cache size\t: 1024 KB");
            lines.Add($"physical id\t: {processor / 4}");
            lines.Add("siblings\t: 4");
            lines.Add($"core id\t\t: {processor % 4 / 2}");
            lines.Add("cpu cores\t: 2");
            lines.Add("");
        }

        return lines;
    }
}
