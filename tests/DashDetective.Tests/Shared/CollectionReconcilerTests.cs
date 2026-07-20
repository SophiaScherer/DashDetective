using DashDetective.Shared;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="CollectionReconciler.Reconcile{TItem, TModel, TKey}"/>: dropping absent
/// rows, updating survivors in place (same instance), inserting new rows at the right index, moving
/// survivors without re-creating them, and matching the incoming order.</summary>
public class CollectionReconcilerTests {
    private sealed class Row {
        public int Id { get; init; }
        public string Value { get; set; } = "";
    }

    private sealed record Model(int Id, string Value);

    private static void Reconcile(ObservableCollection<Row> target, params Model[] incoming) =>
        CollectionReconciler.Reconcile(target, incoming,
            row => row.Id, model => model.Id,
            (row, model) => row.Value = model.Value,
            model => new Row { Id = model.Id, Value = model.Value });

    private static int[] Ids(ObservableCollection<Row> target) => target.Select(r => r.Id).ToArray();

    [Fact]
    public void Reconcile_DropsRowsNoLongerPresent() {
        var target = new ObservableCollection<Row> {
            new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 },
        };

        Reconcile(target, new Model(1, ""), new Model(3, ""));

        Assert.Equal(new[] { 1, 3 }, Ids(target));
    }

    [Fact]
    public void Reconcile_UpdatesSurvivorInPlace_SameInstance() {
        var row = new Row { Id = 1, Value = "a" };
        var target = new ObservableCollection<Row> { row };

        Reconcile(target, new Model(1, "b"));

        Assert.Same(row, target[0]);       // not re-created
        Assert.Equal("b", target[0].Value); // mutated from the model
    }

    [Fact]
    public void Reconcile_InsertsNewModelAtIncomingIndex() {
        var r1 = new Row { Id = 1 };
        var r3 = new Row { Id = 3 };
        var target = new ObservableCollection<Row> { r1, r3 };

        Reconcile(target, new Model(1, ""), new Model(2, "two"), new Model(3, ""));

        Assert.Equal(new[] { 1, 2, 3 }, Ids(target));
        Assert.Same(r1, target[0]);
        Assert.Same(r3, target[2]);
        Assert.Equal("two", target[1].Value);   // the freshly created row
    }

    [Fact]
    public void Reconcile_MovePreservesInstanceIdentity() {
        var r1 = new Row { Id = 1 };
        var r2 = new Row { Id = 2 };
        var target = new ObservableCollection<Row> { r1, r2 };

        Reconcile(target, new Model(2, ""), new Model(1, ""));

        Assert.Equal(new[] { 2, 1 }, Ids(target));
        Assert.Same(r2, target[0]);
        Assert.Same(r1, target[1]);
    }

    [Fact]
    public void Reconcile_ResultingOrderEqualsIncoming() {
        var target = new ObservableCollection<Row> {
            new() { Id = 3 }, new() { Id = 1 }, new() { Id = 2 },
        };

        Reconcile(target, new Model(1, ""), new Model(2, ""), new Model(3, ""));

        Assert.Equal(new[] { 1, 2, 3 }, Ids(target));
    }

    [Fact]
    public void Reconcile_EmptyIncoming_ClearsTarget() {
        var target = new ObservableCollection<Row> { new() { Id = 1 }, new() { Id = 2 } };

        Reconcile(target);

        Assert.Empty(target);
    }
}
