using DashDetective.Shared;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="WidgetCollapse"/>: which widgets are folded, that only a real move is
/// reported (a layout pass re-reading its own state must not write a save), and that a restore is not
/// an edit.</summary>
public class WidgetCollapseTests {
    private static (WidgetCollapse Collapse, List<string> Changes) Build() {
        var collapse = new WidgetCollapse();
        var changes = new List<string>();
        collapse.Changed += id => changes.Add(id);
        return (collapse, changes);
    }

    [Fact]
    public void Set_ANewId_ReportsAChange() {
        var (collapse, _) = Build();

        Assert.True(collapse.Set("settings.alerts", collapsed: true));
        Assert.True(collapse.IsCollapsed("settings.alerts"));
    }

    /// <summary>What stops a panel re-reading its own state from writing a save on every layout pass.</summary>
    [Fact]
    public void Set_TheSameValueTwice_ReportsNoChange() {
        var (collapse, changes) = Build();
        collapse.Set("a", collapsed: true);

        Assert.False(collapse.Set("a", collapsed: true));
        Assert.Equal(["a"], changes);
    }

    [Fact]
    public void Set_RaisesChangedWithTheIdThatMoved() {
        var (collapse, changes) = Build();

        collapse.Set("a", collapsed: true);
        collapse.Set("b", collapsed: true);
        collapse.Set("a", collapsed: false);

        Assert.Equal(["a", "b", "a"], changes);
    }

    [Fact]
    public void Set_WithNoId_DoesNothing() {
        var (collapse, changes) = Build();

        Assert.False(collapse.Set(null, collapsed: true));
        Assert.False(collapse.Set("  ", collapsed: true));
        Assert.Empty(changes);
    }

    [Fact]
    public void IsCollapsed_OfSomethingNeverFolded_IsFalse() {
        var (collapse, _) = Build();

        Assert.False(collapse.IsCollapsed("a"));
        Assert.False(collapse.IsCollapsed(null));
    }

    [Fact]
    public void Expand_AnAlreadyOpenWidget_RaisesNothing() {
        var (collapse, changes) = Build();

        collapse.Expand("a");

        Assert.Empty(changes);
    }

    [Fact]
    public void Expand_OpensAFoldedWidget() {
        var (collapse, _) = Build();
        collapse.Set("a", collapsed: true);

        collapse.Expand("a");

        Assert.False(collapse.IsCollapsed("a"));
    }

    /// <summary>A restore is not an edit: the page it seeds is still being built, and the shell would
    /// otherwise save the state it just loaded.</summary>
    [Fact]
    public void Load_DoesNotRaiseChanged() {
        var (collapse, changes) = Build();

        collapse.Load("a");

        Assert.True(collapse.IsCollapsed("a"));
        Assert.Empty(changes);
    }

    [Fact]
    public void Load_ReplacesWhateverWasFoldedBefore() {
        var (collapse, _) = Build();
        collapse.Set("a", collapsed: true);

        collapse.Load("b");

        Assert.False(collapse.IsCollapsed("a"));
        Assert.True(collapse.IsCollapsed("b"));
    }

    [Fact]
    public void Encode_RoundTripsThroughLoad() {
        var (collapse, _) = Build();
        collapse.Set("a", collapsed: true);
        collapse.Set("b", collapsed: true);

        var (restored, _) = Build();
        restored.Load(collapse.Encode());

        Assert.Equal(collapse.Encode(), restored.Encode());
    }

    /// <summary>The save is debounced on equality, so an unchanged state must encode identically —
    /// which a set's iteration order would not guarantee.</summary>
    [Fact]
    public void Encode_IsStableForTheSameState() {
        var (collapse, _) = Build();
        collapse.Set("b", collapsed: true);
        collapse.Set("a", collapsed: true);

        Assert.Equal(collapse.Encode(), collapse.Encode());
        Assert.Equal("b\u001fa", collapse.Encode());
    }

    [Fact]
    public void Encode_OfNothingFolded_IsEmpty() {
        var (collapse, _) = Build();

        Assert.Equal("", collapse.Encode());
    }
}
