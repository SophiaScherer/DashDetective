using DashDetective.Tabs.FileExplorer;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>
/// Pins that each reason the file list is blank gets its own wording. Three of these — a protected
/// folder, a filter that matched nothing and a genuinely empty folder — used to render as the same
/// "This folder is empty", which is what this class exists to stop coming back.
/// </summary>
public class FolderMessagesTests {
    // A folder that read cleanly and has rows the filter is hiding.
    private static FolderMessage? Resolve(
        bool isReading = false, bool hasFolder = true,
        FolderReadStatus status = FolderReadStatus.Ok,
        int totalCount = 5, int visibleCount = 5, string filterLabel = "Images") =>
        FolderMessages.Resolve(isReading, hasFolder, status, totalCount, visibleCount, filterLabel);

    [Fact]
    public void Resolve_SaysNothingWhileTheFolderIsStillBeingRead() =>
        Assert.Null(Resolve(isReading: true, totalCount: 0, visibleCount: 0));

    [Fact]
    public void Resolve_AReadInFlightOutranksEveryOtherReason() =>
        Assert.Null(Resolve(isReading: true, status: FolderReadStatus.AccessDenied,
                            totalCount: 0, visibleCount: 0));

    [Fact]
    public void Resolve_SaysNothingWhenTheListHasRows() =>
        Assert.Null(Resolve());

    [Fact]
    public void Resolve_AsksForAFolderBeforeAnyIsOpen() {
        var message = Resolve(hasFolder: false, totalCount: 0, visibleCount: 0);

        Assert.Equal("No folder open", message?.Title);
        Assert.Contains("tree", message?.Hint);
    }

    [Fact]
    public void Resolve_SaysDeniedRatherThanEmpty() {
        var message = Resolve(status: FolderReadStatus.AccessDenied, totalCount: 0, visibleCount: 0);

        Assert.Equal("You don't have permission to view this folder", message?.Title);
    }

    [Fact]
    public void Resolve_SaysGoneRatherThanEmpty() {
        var message = Resolve(status: FolderReadStatus.NotFound, totalCount: 0, visibleCount: 0);

        Assert.Equal("This folder no longer exists", message?.Title);
    }

    [Fact]
    public void Resolve_SaysUnreadableRatherThanEmpty() {
        var message = Resolve(status: FolderReadStatus.Unreadable, totalCount: 0, visibleCount: 0);

        Assert.Equal("This folder couldn't be read", message?.Title);
    }

    [Fact]
    public void Resolve_PointsAtShowHiddenWhenThatIsWhatIsHidingEverything() {
        var message = Resolve(status: FolderReadStatus.HiddenOnly, totalCount: 0, visibleCount: 0);

        Assert.Equal("Everything here is hidden", message?.Title);
        Assert.Contains("Show hidden", message?.Hint);
    }

    [Fact]
    public void Resolve_CallsAnEmptyFolderEmpty() {
        var message = Resolve(totalCount: 0, visibleCount: 0);

        Assert.Equal("This folder is empty", message?.Title);
    }

    // The discriminator for the filter case: entries exist, none survived the chip.
    [Fact]
    public void Resolve_NamesTheFilterThatHidEverythingRatherThanSayingEmpty() {
        var message = Resolve(totalCount: 5, visibleCount: 0, filterLabel: "Images");

        Assert.Equal("No Images in this folder", message?.Title);
        Assert.Contains("All", message?.Hint);
    }
}
