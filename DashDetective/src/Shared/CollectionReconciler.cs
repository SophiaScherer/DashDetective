using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DashDetective.Shared;

/// <summary>
/// In-place keyed diff of an already-ordered snapshot into an <see cref="ObservableCollection{T}"/>:
/// removes vanished rows, updates survivors, moves them into their new positions, and inserts new ones.
/// Reusing row instances (rather than clearing and rebuilding) keeps the live tables flicker-free.
/// Shared by the Network connections table and the Processes list.
/// </summary>
public static class CollectionReconciler {
    /// <summary>Reconciles <paramref name="target"/> to match <paramref name="incoming"/> (in order) by
    /// key. <paramref name="update"/> mutates a surviving item from its model; <paramref name="create"/>
    /// builds a new item for a model with no existing row.</summary>
    public static void Reconcile<TItem, TModel, TKey>(
        ObservableCollection<TItem> target, IReadOnlyList<TModel> incoming,
        Func<TItem, TKey> itemKey, Func<TModel, TKey> modelKey,
        Action<TItem, TModel> update, Func<TModel, TItem> create) where TKey : notnull {
        var incomingKeys = new HashSet<TKey>(incoming.Count);
        foreach (var model in incoming)
            incomingKeys.Add(modelKey(model));

        // Drop rows that are no longer present.
        for (var i = target.Count - 1; i >= 0; i--)
            if (!incomingKeys.Contains(itemKey(target[i])))
                target.RemoveAt(i);

        // Index the survivors for O(1) lookup.
        var existing = new Dictionary<TKey, TItem>(target.Count);
        foreach (var item in target)
            existing[itemKey(item)] = item;

        // Walk the incoming order, placing each row at its target index.
        for (var i = 0; i < incoming.Count; i++) {
            var model = incoming[i];
            var key = modelKey(model);
            if (existing.TryGetValue(key, out var item)) {
                update(item, model);
                var current = target.IndexOf(item);
                if (current != i)
                    target.Move(current, i);
            } else {
                var created = create(model);
                existing[key] = created;
                target.Insert(i, created);
            }
        }
    }
}
