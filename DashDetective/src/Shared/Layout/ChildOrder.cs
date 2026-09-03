using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// A panel's ordering of its own children: the settled order, the order being tried mid-drag, and
/// the arithmetic that maps a drop position among the VISIBLE children back into it.
///
/// Index lists, never <c>Children</c> itself — reordering Children is re-entrant mid-layout, detaches
/// a live chart from its feed, and under an ItemsControl fights the item generator. Holds no Avalonia
/// types, so it tests without a layout pass.
/// </summary>
public sealed class ChildOrder {
    private readonly List<int> _order = new();
    private readonly List<int> _preview = new();

    /// <summary>The order to lay out: the one being tried during a drag, the settled one otherwise.</summary>
    public IReadOnlyList<int> Shown(bool previewing) => previewing ? _preview : _order;

    /// <summary>Rebuilds as the declared order when the child count has changed underneath — which is
    /// what an ItemsControl does every time its source does. True when it rebuilt, so the caller can
    /// re-apply whatever order was saved.</summary>
    public bool Sync(int childCount) {
        if (_order.Count == childCount)
            return false;

        _order.Clear();
        for (var i = 0; i < childCount; i++)
            _order.Add(i);
        return true;
    }

    /// <summary>Puts the order back to the declared one, which is what an empty saved order means.
    /// <see cref="Sync"/> cannot do this job: it early-returns whenever the child count is unchanged,
    /// and a reset never changes it. False when it was already declared, so a no-op costs no layout.</summary>
    public bool Reset(int childCount) {
        if (IsDeclared(childCount))
            return false;

        _order.Clear();
        for (var i = 0; i < childCount; i++)
            _order.Add(i);
        return true;
    }

    /// <summary>Starts previewing a reorder from the settled order.</summary>
    public void BeginPreview() {
        _preview.Clear();
        _preview.AddRange(_order);
    }

    /// <summary>Moves a child to this position among the visible ones. False when it is already
    /// there, so a wobble does not re-pack every frame.</summary>
    public bool Move(int child, int visibleTarget, Predicate<int> isVisible) {
        var from = _preview.IndexOf(child);
        if (from < 0)
            return false;

        _preview.RemoveAt(from);
        var to = PositionOf(visibleTarget, isVisible);
        _preview.Insert(to, child);
        return to != from;
    }

    /// <summary>Keeps the previewed order and reports the ids in it. A child with no id contributes
    /// nothing; a collapsed one still does, so hiding a widget cannot drop it out of the save.</summary>
    public IReadOnlyList<string> Commit(IReadOnlyList<string> declared) {
        _order.Clear();
        _order.AddRange(_preview);

        var ids = new List<string>(_order.Count);
        foreach (var child in _order)
            if (child < declared.Count && declared[child].Length > 0)
                ids.Add(declared[child]);
        return ids;
    }

    /// <summary>Reorders to a saved order, keeping any child the save does not name at its declared
    /// position. False when there is nothing to apply.</summary>
    public bool ApplySaved(IReadOnlyList<string>? saved, IReadOnlyList<string> declared) {
        if (saved is null || saved.Count == 0 || declared.Count == 0)
            return false;

        Sync(declared.Count);
        var resolved = WidgetOrders.Resolve(Named(declared), saved);
        var position = new Dictionary<string, int>(resolved.Count);
        for (var i = 0; i < resolved.Count; i++)
            position[resolved[i]] = i;

        _order.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        return true;

        // A child the save does not name ranks by its declared index, so it cannot be dragged past.
        double Rank(int child) =>
            child < declared.Count && declared[child].Length > 0
            && position.TryGetValue(declared[child], out var rank)
                ? rank
                : child;
    }

    /// <summary>Where in the previewed order a child dropped at this position among the VISIBLE ones
    /// belongs. The two part company the moment a child is collapsed: the order holds every child,
    /// while a drop index counts only what is on screen.</summary>
    private int PositionOf(int visibleTarget, Predicate<int> isVisible) {
        var seen = 0;
        for (var i = 0; i < _preview.Count; i++) {
            if (!isVisible(_preview[i]))
                continue;
            if (seen == visibleTarget)
                return i;
            seen++;
        }
        return _preview.Count;
    }

    /// <summary>Whether the order is already the declared one, top to bottom.</summary>
    private bool IsDeclared(int childCount) {
        if (_order.Count != childCount)
            return false;

        for (var i = 0; i < _order.Count; i++)
            if (_order[i] != i)
                return false;
        return true;
    }

    private static List<string> Named(IReadOnlyList<string> declared) {
        var named = new List<string>(declared.Count);
        foreach (var id in declared)
            if (id.Length > 0)
                named.Add(id);
        return named;
    }
}
