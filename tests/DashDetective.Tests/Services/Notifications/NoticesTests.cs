using DashDetective.Services.Notifications;
using Xunit;

namespace DashDetective.Tests.Services.Notifications;

/// <summary>Covers <see cref="Notices"/>: the confirmation copy. Pinned because four call sites announce
/// a saved export and the wording has to be the same one every time.</summary>
public class NoticesTests {
    [Fact]
    public void Exported_NamesThePathTheFileWasWrittenTo() =>
        Assert.Equal(@"Exported to C:\reports\system.html",
                     Notices.Exported(@"C:\reports\system.html"));

    /// <summary>The row already shows the key, so the confirmation names the action instead.</summary>
    [Fact]
    public void ShortcutRestored_NamesTheAction() =>
        Assert.Equal("Shortcut restored for Refresh", Notices.ShortcutRestored("Refresh"));
}
