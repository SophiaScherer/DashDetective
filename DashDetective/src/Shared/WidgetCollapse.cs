using DashDetective.Shared.Controls;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared;

/// <summary>
/// Which of a page's widgets are folded shut, and a signal when one moves. A page owns one of these
/// and hands it to every <c>WidgetPanel</c> that may fold; holding the state here rather than on the
/// panel is what lets it survive a restart and lets the page reopen a card it needs on screen.
///
/// No Avalonia types, so it tests without a layout pass — the same split <see cref="SavedOrder"/>
/// makes against the panel that lays a strip out.
/// </summary>
public sealed class WidgetCollapse {
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();

    /// <summary>Raised with the id that moved, so a panel can tell whether the change was its own.</summary>
    public event Action<string>? Changed;

    public bool IsCollapsed(string? id) => id is not null && _collapsed.Contains(id);

    /// <summary>Folds or unfolds a widget. False when nothing moved, which is what stops a layout pass
    /// re-reading its own state from writing a save.</summary>
    public bool Set(string? id, bool collapsed) {
        if (string.IsNullOrWhiteSpace(id) || collapsed == _collapsed.Contains(id))
            return false;

        if (collapsed) {
            _collapsed.Add(id);
            _order.Add(id);
        } else {
            _collapsed.Remove(id);
            _order.Remove(id);
        }

        Changed?.Invoke(id);
        return true;
    }

    /// <summary>Opens a widget, if it was folded. What a search reveal calls before jumping to a row:
    /// a folded card's body is never measured, so its rows are not in the visual tree to find.</summary>
    public void Expand(string? id) => Set(id, collapsed: false);

    /// <summary>Restores a saved state. Does not raise <see cref="Changed"/> — it is a restore, not an
    /// edit, and the page it seeds is still being built.</summary>
    public void Load(string? encoded) {
        _collapsed.Clear();
        _order.Clear();
        foreach (var id in CollapsedWidgets.Decode(encoded))
            if (_collapsed.Add(id))
                _order.Add(id);
    }

    /// <summary>The folded ids as one persistable string, in a stable order so an unchanged state
    /// cannot churn the debounced save.</summary>
    public string Encode() => CollapsedWidgets.Encode(_order);
}
