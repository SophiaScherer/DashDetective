using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DashDetective.Tabs.FileExplorer;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can be refilled wholesale in one notification.
/// Clearing and re-adding a 5,000-entry folder one item at a time costs ~10,000 CollectionChanged
/// notifications on the UI thread, each invalidating layout; <see cref="Reset"/> raises exactly one.
/// Ordinary mutation is left alone — the tree's in-place merge still inserts and removes item by item
/// to preserve node instances.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T> {
    // The name bindings listen for when a collection's items change wholesale.
    private const string IndexerName = "Item[]";

    /// <summary>Replaces every item, raising a single Reset rather than one event per item.</summary>
    public void Reset(IEnumerable<T> items) {
        CheckReentrancy();

        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs(IndexerName));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
