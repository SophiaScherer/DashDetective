using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers <see cref="PathCompletion"/>: only the last path segment is completed, the folder it
/// reads is cached so typing a name doesn't re-enumerate once per keystroke, and an unreadable folder
/// yields no suggestion rather than an exception.</summary>
public class PathCompletionTests {
    private sealed class FakeDirectory {
        private readonly Dictionary<string, string[]> _folders;

        public FakeDirectory(Dictionary<string, string[]> folders) => _folders = folders;

        public int Reads { get; private set; }
        public Exception? Failure { get; set; }

        public Task<IReadOnlyList<DirEntry>> ReadAsync(string path, bool includeHidden) {
            Reads++;
            if (Failure is { } failure)
                return Task.FromException<IReadOnlyList<DirEntry>>(failure);

            var names = _folders.TryGetValue(path, out var found) ? found : [];
            var entries = new List<DirEntry>(names.Length);
            foreach (var name in names)
                entries.Add(new DirEntry(name, path + name, false));

            return Task.FromResult<IReadOnlyList<DirEntry>>(entries);
        }
    }

    private static FakeDirectory Disk() => new(new Dictionary<string, string[]> {
        [TestPaths.Dir()] = ["Users", "Windows", "Program Files"],
        [TestPaths.Dir("Users")] = ["Sophia", "Public"],
        [TestPaths.Dir("Users", "Sophia")] = ["Documents", "Downloads", "Desktop"],
    });

    private static PathCompletion Completion(FakeDirectory disk) => new(disk.ReadAsync);

    // ----- Splitting -----

    [Fact]
    public void TrySplit_SeparatesTheFolderToReadFromTheNameToComplete() {
        AssertSplit(TestPaths.Dir() + "Us", TestPaths.Dir(), "Us");
        AssertSplit(TestPaths.Dir("Users") + "Soph", TestPaths.Dir("Users"), "Soph");

        // The alternate separator splits too (on Unix it is the same character as the primary one).
        var alt = TestPaths.Root + "Users" + Path.AltDirectorySeparatorChar;
        AssertSplit(alt + "Soph", alt, "Soph");
    }

    private static void AssertSplit(string typed, string expectedParent, string expectedStub) {
        Assert.True(PathCompletion.TrySplit(typed, out var parent, out var stub));
        Assert.Equal(expectedParent, parent);
        Assert.Equal(expectedStub, stub);
    }

    [Fact]
    public void TrySplit_ReportsNothingToComplete() {
        Assert.False(PathCompletion.TrySplit(TestPaths.Dir(), out _, out _));         // nothing typed
        Assert.False(PathCompletion.TrySplit(TestPaths.Dir("Users"), out _, out _));  // after the separator
        Assert.False(PathCompletion.TrySplit("Documents", out _, out _));  // no separator, no folder to read
        Assert.False(PathCompletion.TrySplit("", out _, out _));
        Assert.False(PathCompletion.TrySplit(null, out _, out _));
    }

    // ----- Completing -----

    [Fact]
    public async Task CompleteAsync_CompletesTheLastSegmentOntoTheFullPath() {
        Assert.Equal(TestPaths.Of("Users"),
            await Completion(Disk()).CompleteAsync(TestPaths.Dir() + "Us", false));
    }

    [Fact]
    public async Task CompleteAsync_CompletesADeeperSegment() {
        Assert.Equal(TestPaths.Of("Users", "Sophia", "Documents"),
            await Completion(Disk()).CompleteAsync(TestPaths.Dir("Users", "Sophia") + "Docu", false));
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingWhenSiblingsDisagree() {
        // Documents, Downloads and Desktop share only the "D" already typed.
        Assert.Null(await Completion(Disk()).CompleteAsync(TestPaths.Dir("Users", "Sophia") + "D", false));
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingForAnUnknownName() {
        Assert.Null(await Completion(Disk()).CompleteAsync(TestPaths.Dir() + "zzz", false));
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingWithNoSegmentToComplete() {
        var disk = Disk();

        Assert.Null(await Completion(disk).CompleteAsync(TestPaths.Dir(), false));
        Assert.Equal(0, disk.Reads);
    }

    [Fact]
    public async Task CompleteAsync_ReadsAFolderOncePerRunOfKeystrokes() {
        var disk = Disk();
        var completion = Completion(disk);
        var sophia = TestPaths.Dir("Users", "Sophia");

        await completion.CompleteAsync(sophia + "D", false);
        await completion.CompleteAsync(sophia + "Do", false);
        await completion.CompleteAsync(sophia + "Doc", false);

        Assert.Equal(1, disk.Reads);
    }

    [Fact]
    public async Task CompleteAsync_RereadsWhenTheCaretMovesToAnotherFolder() {
        var disk = Disk();
        var completion = Completion(disk);

        await completion.CompleteAsync(TestPaths.Dir() + "Us", false);
        await completion.CompleteAsync(TestPaths.Dir("Users") + "So", false);

        Assert.Equal(2, disk.Reads);
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingWhenTheFolderCannotBeRead() {
        // A typo mid-path shouldn't throw once per keystroke.
        var disk = Disk();
        disk.Failure = new UnauthorizedAccessException();

        Assert.Null(await Completion(disk).CompleteAsync(TestPaths.Dir() + "Us", false));
    }
}
