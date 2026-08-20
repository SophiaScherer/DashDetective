using DashDetective.Tabs.FileExplorer;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers <see cref="BulkObservableCollection{T}"/>: that a wholesale refill costs exactly one
/// Reset notification (the whole reason the type exists — a per-item Clear/Add on a 5,000-entry folder
/// froze the file list), that the contents afterwards are the source's, and that the Count and indexer
/// property notices bindings rely on are still raised.</summary>
public class BulkObservableCollectionTests {
    [Fact]
    public void Reset_ReplacesContents() {
        var collection = new BulkObservableCollection<string> { "old" };

        collection.Reset(new[] { "a", "b", "c" });

        Assert.Equal(new[] { "a", "b", "c" }, collection);
    }

    [Fact]
    public void Reset_RaisesOneResetNotification() {
        var collection = new BulkObservableCollection<int> { 1, 2 };
        var events = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, e) => events.Add(e);

        collection.Reset(new[] { 3, 4, 5, 6 });

        var raised = Assert.Single(events);
        Assert.Equal(NotifyCollectionChangedAction.Reset, raised.Action);
    }

    [Fact]
    public void Reset_RaisesCountAndIndexerNotices() {
        var collection = new BulkObservableCollection<int>();
        var names = new List<string?>();
        ((INotifyPropertyChanged)collection).PropertyChanged += (_, e) => names.Add(e.PropertyName);

        collection.Reset(new[] { 1, 2 });

        Assert.Contains("Count", names);
        Assert.Contains("Item[]", names);
    }

    [Fact]
    public void Reset_EmptySource_ClearsCollection() {
        var collection = new BulkObservableCollection<string> { "a", "b" };

        collection.Reset(new string[0]);

        Assert.Empty(collection);
    }

    [Fact]
    public void Reset_LeavesOrdinaryMutationWorking() {
        var collection = new BulkObservableCollection<string>();
        collection.Reset(new[] { "a", "c" });

        collection.Insert(1, "b");
        collection.RemoveAt(0);

        Assert.Equal(new[] { "b", "c" }, collection);
    }
}
