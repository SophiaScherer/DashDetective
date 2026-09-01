using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace DashDetective.Shared;

/// <summary>
/// One reorderable strip's saved order: the key it persists under, the ids in display order, and a
/// signal when a drag changes them. Bound two-way to the panel that lays the strip out.
///
/// A page owns one of these per strip rather than one order outright, because a page can hold more
/// than one — Performance reorders its device rail and its stat tiles separately, and the tiles keep
/// a different order per device kind.
/// </summary>
public sealed class SavedOrder : ObservableObject {
    private IReadOnlyList<string> _order = [];

    /// <param name="key">What this order is saved under. Unique across the app, not just the page.</param>
    public SavedOrder(string key) => Key = key;

    public string Key { get; }

    /// <summary>The ids in display order. Empty until a panel reports one.</summary>
    public IReadOnlyList<string> Order {
        get => _order;
        set {
            if (ReferenceEquals(_order, value))
                return;
            _order = value ?? [];
            OnPropertyChanged();
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when the order changes, so the shell can persist it.</summary>
    public event Action? Changed;
}
