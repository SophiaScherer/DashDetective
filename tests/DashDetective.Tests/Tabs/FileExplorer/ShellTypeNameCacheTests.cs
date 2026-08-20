using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers <see cref="ShellTypeNameCache"/>: that the shell is asked once per extension and once
/// for directories however many entries share them — the whole reason the type exists, since a folder of
/// 5,000 files was otherwise 5,000 shell round-trips — while every entry still gets its own name.</summary>
public class ShellTypeNameCacheTests {
    [Fact]
    public void NameFor_SameExtension_AsksTheShellOnce() {
        var shell = new FakeShellInterop();
        var cache = new ShellTypeNameCache(shell);

        cache.NameFor(@"C:\f\a.txt", false);
        cache.NameFor(@"C:\f\b.txt", false);
        cache.NameFor(@"C:\f\c.TXT", false);

        Assert.Single(shell.TypeNameCalls);
    }

    [Fact]
    public void NameFor_DifferentExtensions_AsksTheShellForEach() {
        var shell = new FakeShellInterop();
        var cache = new ShellTypeNameCache(shell);

        Assert.Equal("TXT File", cache.NameFor(@"C:\f\a.txt", false));
        Assert.Equal("PDF File", cache.NameFor(@"C:\f\b.pdf", false));
        Assert.Equal("File", cache.NameFor(@"C:\f\LICENSE", false));

        Assert.Equal(3, shell.TypeNameCalls.Count);
    }

    [Fact]
    public void NameFor_Directories_AsksTheShellOnceForAllOfThem() {
        var shell = new FakeShellInterop();
        var cache = new ShellTypeNameCache(shell);

        Assert.Equal("File folder", cache.NameFor(@"C:\f\one", true));
        Assert.Equal("File folder", cache.NameFor(@"C:\f\two.d", true));

        Assert.Single(shell.TypeNameCalls);
    }

    [Fact]
    public void NameFor_DirectoryAndFile_AreCachedSeparately() {
        var shell = new FakeShellInterop();
        var cache = new ShellTypeNameCache(shell);

        Assert.Equal("File folder", cache.NameFor(@"C:\f\a.txt", true));
        Assert.Equal("TXT File", cache.NameFor(@"C:\f\b.txt", false));

        Assert.Equal(2, shell.TypeNameCalls.Count);
    }
}
