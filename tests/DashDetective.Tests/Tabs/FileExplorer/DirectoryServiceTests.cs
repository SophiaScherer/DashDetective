using DashDetective.Tabs.FileExplorer;
using DashDetective.Tests.Fakes;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>
/// Pins that an empty folder listing says why it is empty. <c>IgnoreInaccessible</c> suppresses the
/// failure to open the folder itself, so without this the four situations the File Explorer has to
/// tell apart all arrive as the same successful, empty list.
///
/// <see cref="FolderReadStatus.AccessDenied"/> is not covered: there is no portable way to create a
/// folder the test process cannot read (Windows needs an ACL edit, Linux a second user), so it is
/// checked by hand against C:\System Volume Information instead.
/// </summary>
public class DirectoryServiceTests : IDisposable {
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "DashDetectiveTests_" + Path.GetRandomFileName());

    public DirectoryServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose() {
        try {
            Directory.Delete(_dir, recursive: true);
        } catch {
            // A leftover temp folder is not worth failing a test over.
        }
        GC.SuppressFinalize(this);
    }

    private Task<FolderRead> Read(bool includeHidden = false) =>
        DirectoryService.GetEntriesAsync(_dir, includeHidden, new FakeShellInterop());

    [Fact]
    public async Task GetEntriesAsync_ReportsOkForAFolderWithEntries() {
        File.WriteAllText(Path.Combine(_dir, "note.txt"), "hello");

        var read = await Read();

        Assert.Equal(FolderReadStatus.Ok, read.Status);
        Assert.Single(read.Items);
    }

    [Fact]
    public async Task GetEntriesAsync_ReportsOkForAGenuinelyEmptyFolder() {
        var read = await Read();

        Assert.Equal(FolderReadStatus.Ok, read.Status);
        Assert.Empty(read.Items);
    }

    [Fact]
    public async Task GetEntriesAsync_ReportsNotFoundForAFolderThatIsGone() {
        var read = await DirectoryService.GetEntriesAsync(
            Path.Combine(_dir, "no-such-folder"), false, new FakeShellInterop());

        Assert.Equal(FolderReadStatus.NotFound, read.Status);
        Assert.Empty(read.Items);
    }

    [Fact]
    public async Task GetEntriesAsync_SeparatesAHiddenOnlyFolderFromAnEmptyOne() {
        var file = Path.Combine(_dir, ".hidden");
        File.WriteAllText(file, "hello");
        File.SetAttributes(file, FileAttributes.Hidden);

        var read = await Read();

        Assert.Equal(FolderReadStatus.HiddenOnly, read.Status);
        Assert.Empty(read.Items);
    }

    [Fact]
    public async Task GetEntriesAsync_ShowsTheHiddenEntriesWhenAskedTo() {
        var file = Path.Combine(_dir, ".hidden");
        File.WriteAllText(file, "hello");
        File.SetAttributes(file, FileAttributes.Hidden);

        var read = await Read(includeHidden: true);

        Assert.Equal(FolderReadStatus.Ok, read.Status);
        Assert.Single(read.Items);
    }
}
