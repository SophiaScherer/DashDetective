using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="SocketInodeMap"/>: the inode extraction that has to survive however the
/// filesystem seam renders a link target, the caching that keeps a 2.5 s poll from re-walking every
/// process, and the first-writer rule the UI's keyed diff depends on.</summary>
public class SocketInodeMapTests {
    /// <summary>A process holding two sockets and a regular file, plus a second process — the shape of a
    /// real <c>/proc/[pid]/fd</c>.</summary>
    private static FakeProcFileSystem Tree() =>
        new FakeProcFileSystem()
            .WithLink("/proc/812/fd/0", "/dev/null")
            .WithLink("/proc/812/fd/3", "/proc/812/fd/socket:[21344]")
            .WithLink("/proc/812/fd/4", "/proc/812/fd/pipe:[21400]")
            .WithLink("/proc/1201/fd/7", "/proc/1201/fd/socket:[48219]")
            .WithLink("/proc/1201/fd/9", "/proc/1201/fd/anon_inode:[eventpoll]");

    [Fact]
    public void Refresh_MapsSocketInodesToTheirProcess() {
        var map = new SocketInodeMap(Tree());

        map.Refresh([21344, 48219]);

        Assert.Equal(812, map.PidFor(21344));
        Assert.Equal(1201, map.PidFor(48219));
    }

    /// <summary>Only sockets. A pipe and an eventfd carry the same <c>name:[inode]</c> shape, so matching on
    /// the brackets alone would attribute a connection to whichever process happened to hold a pipe with a
    /// colliding number.</summary>
    [Fact]
    public void Refresh_IgnoresNonSocketDescriptors() {
        var map = new SocketInodeMap(Tree());

        map.Refresh([21400]);

        Assert.Equal(SocketInodeMap.NoPid, map.PidFor(21400));
    }

    /// <summary>An unknown inode — another user's socket, whose <c>fd</c> directory is unlistable — resolves
    /// to no PID rather than to a wrong one.</summary>
    [Fact]
    public void PidFor_UnknownInode_ReportsNoPid() {
        var map = new SocketInodeMap(Tree());

        map.Refresh([21344]);

        Assert.Equal(SocketInodeMap.NoPid, map.PidFor(99999));
    }

    /// <summary>The whole reason the map is cached: the connections table polls every 2.5 s, and the walk is
    /// a readlink per descriptor across every process. A poll asking only about inodes already known must not
    /// walk — observed by staging a socket that appears afterwards and would be picked up if it did.</summary>
    [Fact]
    public void Refresh_AllInodesKnown_DoesNotWalkAgain() {
        var proc = Tree();
        var map = new SocketInodeMap(proc);
        map.Refresh([21344, 48219]);

        proc.WithLink("/proc/1500/fd/3", "socket:[77777]");
        map.Refresh([21344, 48219]);

        Assert.Equal(SocketInodeMap.NoPid, map.PidFor(77777));
        Assert.Equal(812, map.PidFor(21344));
    }

    /// <summary>Inode 0 is the kernel's "no inode" — a TIME_WAIT slot. Asking about it must not trigger a
    /// walk, or every poll would rebuild the map for sockets nothing can own.</summary>
    [Fact]
    public void Refresh_OnlyInodeZero_DoesNotWalk() {
        var map = new SocketInodeMap(Tree());

        map.Refresh([0]);

        // Nothing was mapped, so no walk happened — a walk would have found both of the tree's sockets.
        Assert.Equal(SocketInodeMap.NoPid, map.PidFor(0));
        Assert.Equal(SocketInodeMap.NoPid, map.PidFor(21344));
    }

    /// <summary>A rebuild is wholesale, so an inode whose socket has closed drops out instead of pinning a
    /// stale PID — which matters because Linux reuses PIDs, so a stale entry would eventually name a real
    /// but unrelated process.</summary>
    [Fact]
    public void Refresh_Rebuild_DropsClosedSockets() {
        var proc = Tree();
        var map = new SocketInodeMap(proc);
        map.Refresh([21344, 48219]);
        Assert.Equal(812, map.PidFor(21344));

        // 812's socket closes; a new one elsewhere is what forces the next walk.
        proc.WithoutLink("/proc/812/fd/3").WithLink("/proc/1500/fd/3", "socket:[77777]");
        map.Refresh([77777]);

        Assert.Equal(1500, map.PidFor(77777));
        Assert.Equal(SocketInodeMap.NoPid, map.PidFor(21344));
    }

    /// <summary>
    /// The production <c>IProcFileSystem</c> resolves a link target to a full path, and
    /// <c>socket:[12345]</c> is not one — so it comes back joined to the descriptor's own directory. The
    /// bare form is what a naive reading assumes. Both must work, because this is the one thing in the
    /// milestone a Windows test cannot actually observe.
    /// </summary>
    [Theory]
    [InlineData("socket:[21344]", 21344L)]
    [InlineData("/proc/812/fd/socket:[21344]", 21344L)]
    public void InodeOf_ReadsEitherFormOfLinkTarget(string target, long expected) =>
        Assert.Equal(expected, SocketInodeMap.InodeOf(target));

    [Theory]
    [InlineData("/dev/null")]
    [InlineData("pipe:[21400]")]
    [InlineData("anon_inode:[eventpoll]")]
    [InlineData("socket:[")]
    [InlineData("socket:[notanumber]")]
    public void InodeOf_RejectsWhatIsNotASocketInode(string target) =>
        Assert.Null(SocketInodeMap.InodeOf(target));

    /// <summary>A socket held by two processes (a fork, or one passed over a unix socket) resolves to the
    /// lowest PID. The row's identity key carries the PID, so the choice has to be stable — and "whichever
    /// the walk saw first" is not, because <c>/proc</c> listing order is unspecified. 1201 sorts before 812
    /// as a string, which is what this fixture pins.</summary>
    [Fact]
    public void Refresh_SharedSocket_KeepsTheLowestPid() {
        var proc = new FakeProcFileSystem()
            .WithLink("/proc/812/fd/3", "socket:[21344]")
            .WithLink("/proc/1201/fd/3", "socket:[21344]");
        var map = new SocketInodeMap(proc);

        map.Refresh([21344]);

        Assert.Equal(812, map.PidFor(21344));
    }
}
