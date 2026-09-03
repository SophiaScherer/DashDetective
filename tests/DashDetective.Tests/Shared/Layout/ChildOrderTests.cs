using DashDetective.Shared.Layout;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="ChildOrder"/>: the permutation both reorderable panels share, and the
/// gap between a drop index — which counts only what is on screen — and an order that holds every
/// child, collapsed ones included.</summary>
public class ChildOrderTests {
    private static readonly Predicate<int> AllVisible = _ => true;

    /// <summary>The order as ids, so a test reads as the strip the user sees.</summary>
    private static string Shown(ChildOrder order, bool previewing, params string[] declared) {
        var ids = new List<string>();
        foreach (var child in order.Shown(previewing))
            ids.Add(declared[child]);
        return string.Join(" ", ids);
    }

    private static ChildOrder Order(int children) {
        var order = new ChildOrder();
        order.Sync(children);
        return order;
    }

    // ===== Sync =====

    [Fact]
    public void Sync_BuildsTheDeclaredOrder() {
        var order = Order(3);
        Assert.Equal([0, 1, 2], order.Shown(previewing: false));
    }

    [Fact]
    public void Sync_ReportsOnlyARebuild() {
        var order = Order(3);
        Assert.False(order.Sync(3));
        Assert.True(order.Sync(4));
    }

    // ===== Resetting =====

    [Fact]
    public void Reset_PutsAPermutedOrderBackToDeclared() {
        var order = Order(3);
        order.ApplySaved(["c", "a", "b"], ["a", "b", "c"]);

        Assert.True(order.Reset(3));

        Assert.Equal("a b c", Shown(order, previewing: false, "a", "b", "c"));
    }

    /// <summary>What stops a reset costing a layout pass on every panel that was never dragged.</summary>
    [Fact]
    public void Reset_AlreadyDeclared_ReportsNoChange() {
        Assert.False(Order(3).Reset(3));
    }

    [Fact]
    public void Reset_AChildCountItHasNotSeen_BuildsTheDeclaredOrder() {
        var order = new ChildOrder();

        Assert.True(order.Reset(3));

        Assert.Equal([0, 1, 2], order.Shown(previewing: false));
    }

    /// <summary>Sync early-returns on an unchanged count, so the next measure must not undo a reset.</summary>
    [Fact]
    public void Reset_ThenSync_KeepsTheDeclaredOrder() {
        var order = Order(3);
        order.ApplySaved(["c", "a", "b"], ["a", "b", "c"]);
        order.Reset(3);

        Assert.False(order.Sync(3));
        Assert.Equal("a b c", Shown(order, previewing: false, "a", "b", "c"));
    }

    // ===== Moving =====

    [Fact]
    public void Move_TakesTheChildToThatPosition() {
        var order = Order(3);
        order.BeginPreview();

        Assert.True(order.Move(child: 0, visibleTarget: 2, AllVisible));

        Assert.Equal("b c a", Shown(order, previewing: true, "a", "b", "c"));
    }

    [Fact]
    public void Move_WhereItAlreadyIs_IsNoMove() {
        var order = Order(3);
        order.BeginPreview();
        Assert.False(order.Move(child: 1, visibleTarget: 1, AllVisible));
    }

    [Fact]
    public void Move_LeavesTheSettledOrderAloneUntilCommitted() {
        var order = Order(3);
        order.BeginPreview();
        order.Move(child: 2, visibleTarget: 0, AllVisible);

        Assert.Equal("c a b", Shown(order, previewing: true, "a", "b", "c"));
        Assert.Equal("a b c", Shown(order, previewing: false, "a", "b", "c"));
    }

    /// <summary>The load-bearing case: a drop index counts the widgets on screen, while the order it
    /// lands in also holds the collapsed one. Dropping at visible position 1 must land before "c",
    /// not before the hidden "b".</summary>
    [Fact]
    public void Move_CountsOnlyTheVisibleChildren() {
        var order = Order(4);                     // a, b (hidden), c, d
        order.BeginPreview();

        order.Move(child: 3, visibleTarget: 1, child => child != 1);

        Assert.Equal("a b d c", Shown(order, previewing: true, "a", "b", "c", "d"));
    }

    [Fact]
    public void Move_PastEveryVisibleChild_LandsLast() {
        var order = Order(3);
        order.BeginPreview();

        order.Move(child: 0, visibleTarget: 5, AllVisible);

        Assert.Equal("b c a", Shown(order, previewing: true, "a", "b", "c"));
    }

    // ===== Commit =====

    [Fact]
    public void Commit_KeepsThePreviewAndReportsItsIds() {
        var order = Order(3);
        order.BeginPreview();
        order.Move(child: 0, visibleTarget: 2, AllVisible);

        var ids = order.Commit(["a", "b", "c"]);

        Assert.Equal(["b", "c", "a"], ids);
        Assert.Equal("b c a", Shown(order, previewing: false, "a", "b", "c"));
    }

    /// <summary>A child with no id is a card strip or a pinned row: it holds its place but is not
    /// worth naming in a save.</summary>
    [Fact]
    public void Commit_SkipsAChildWithNoId() {
        var order = Order(3);
        order.BeginPreview();
        Assert.Equal(["a", "c"], order.Commit(["a", "", "c"]));
    }

    // ===== Applying a saved order =====

    [Fact]
    public void ApplySaved_ReordersToTheSavedIds() {
        var order = Order(3);

        Assert.True(order.ApplySaved(["c", "a", "b"], ["a", "b", "c"]));

        Assert.Equal("c a b", Shown(order, previewing: false, "a", "b", "c"));
    }

    /// <summary>"a" is new since the save, so it keeps the place its author declared it in — ahead of
    /// the pair the user arranged — rather than being appended below a layout they set once.</summary>
    [Fact]
    public void ApplySaved_KeepsAWidgetTheSaveDoesNotName() {
        var order = Order(3);

        order.ApplySaved(["c", "b"], ["a", "b", "c"]);

        Assert.Equal("a c b", Shown(order, previewing: false, "a", "b", "c"));
    }

    [Fact]
    public void ApplySaved_WithNothingSaved_DoesNothing() {
        var order = Order(2);
        Assert.False(order.ApplySaved(null, ["a", "b"]));
        Assert.False(order.ApplySaved([], ["a", "b"]));
    }

    [Fact]
    public void ApplySaved_SyncsAChildCountItHasNotSeen() {
        var order = new ChildOrder();
        Assert.True(order.ApplySaved(["c", "b", "a"], ["a", "b", "c"]));
        Assert.Equal("c b a", Shown(order, previewing: false, "a", "b", "c"));
    }
}
