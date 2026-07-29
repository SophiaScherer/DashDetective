using DashDetective.Tabs.FileExplorer;
using System;
using System.Collections.Generic;
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
        [@"C:\"] = ["Users", "Windows", "Program Files"],
        [@"C:\Users\"] = ["Sophia", "Public"],
        [@"C:\Users\Sophia\"] = ["Documents", "Downloads", "Desktop"],
    });

    private static PathCompletion Completion(FakeDirectory disk) => new(disk.ReadAsync);

    // ----- Splitting -----

    [Theory]
    [InlineData(@"C:\Us", @"C:\", "Us")]
    [InlineData(@"C:\Users\Soph", @"C:\Users\", "Soph")]
    [InlineData(@"C:/Users/Soph", "C:/Users/", "Soph")]
    public void TrySplit_SeparatesTheFolderToReadFromTheNameToComplete(
        string typed, string expectedParent, string expectedStub) {
        Assert.True(PathCompletion.TrySplit(typed, out var parent, out var stub));
        Assert.Equal(expectedParent, parent);
        Assert.Equal(expectedStub, stub);
    }

    [Theory]
    [InlineData(@"C:\")]        // nothing typed after the separator
    [InlineData(@"C:\Users\")]
    [InlineData("Documents")]   // no separator names no folder to read
    [InlineData("")]
    [InlineData(null)]
    public void TrySplit_ReportsNothingToComplete(string? typed) {
        Assert.False(PathCompletion.TrySplit(typed, out _, out _));
    }

    // ----- Completing -----

    [Fact]
    public async Task CompleteAsync_CompletesTheLastSegmentOntoTheFullPath() {
        Assert.Equal(@"C:\Users", await Completion(Disk()).CompleteAsync(@"C:\Us", false));
    }

    [Fact]
    public async Task CompleteAsync_CompletesADeeperSegment() {
        Assert.Equal(@"C:\Users\Sophia\Documents",
            await Completion(Disk()).CompleteAsync(@"C:\Users\Sophia\Docu", false));
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingWhenSiblingsDisagree() {
        // Documents, Downloads and Desktop share only the "D" already typed.
        Assert.Null(await Completion(Disk()).CompleteAsync(@"C:\Users\Sophia\D", false));
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingForAnUnknownName() {
        Assert.Null(await Completion(Disk()).CompleteAsync(@"C:\zzz", false));
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingWithNoSegmentToComplete() {
        var disk = Disk();

        Assert.Null(await Completion(disk).CompleteAsync(@"C:\", false));
        Assert.Equal(0, disk.Reads);
    }

    [Fact]
    public async Task CompleteAsync_ReadsAFolderOncePerRunOfKeystrokes() {
        var disk = Disk();
        var completion = Completion(disk);

        await completion.CompleteAsync(@"C:\Users\Sophia\D", false);
        await completion.CompleteAsync(@"C:\Users\Sophia\Do", false);
        await completion.CompleteAsync(@"C:\Users\Sophia\Doc", false);

        Assert.Equal(1, disk.Reads);
    }

    [Fact]
    public async Task CompleteAsync_RereadsWhenTheCaretMovesToAnotherFolder() {
        var disk = Disk();
        var completion = Completion(disk);

        await completion.CompleteAsync(@"C:\Us", false);
        await completion.CompleteAsync(@"C:\Users\So", false);

        Assert.Equal(2, disk.Reads);
    }

    [Fact]
    public async Task CompleteAsync_SuggestsNothingWhenTheFolderCannotBeRead() {
        // A typo mid-path shouldn't throw once per keystroke.
        var disk = Disk();
        disk.Failure = new UnauthorizedAccessException();

        Assert.Null(await Completion(disk).CompleteAsync(@"C:\Us", false));
    }
}
