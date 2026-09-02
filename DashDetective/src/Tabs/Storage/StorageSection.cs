using DashDetective.Shared.Controls;

namespace DashDetective.Tabs.Storage;

/// <summary>
/// One of the Storage page's own panels, as an item the board can lay out. The drive cards are items
/// already — they are the drives — but the two panels are markup, so they need something to be: a
/// marker naming the panel and carrying the page its bindings read, since a board child's DataContext
/// is its item rather than the page.
///
/// That is what lets a drive card sit beside or below a panel instead of being stuck in a strip of
/// its own: everything on the page is one list, in one order.
/// </summary>
public abstract class StorageSection : IWidgetIdentity {
    protected StorageSection(StorageViewModel page) => Page = page;

    /// <summary>The page the panel's markup binds through.</summary>
    public StorageViewModel Page { get; }

    public abstract string? WidgetId { get; }
}

/// <summary>The partitions table.</summary>
public sealed class PartitionsSection : StorageSection {
    public PartitionsSection(StorageViewModel page) : base(page) { }

    public override string? WidgetId => "storage.partitions";
}

/// <summary>The disk activity chart and its readouts.</summary>
public sealed class ActivitySection : StorageSection {
    public ActivitySection(StorageViewModel page) : base(page) { }

    public override string? WidgetId => "storage.activity";
}
