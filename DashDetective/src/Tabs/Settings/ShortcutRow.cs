using CommunityToolkit.Mvvm.ComponentModel;
using DashDetective.Shared.Shortcuts;
using System.Collections.Generic;

namespace DashDetective.Tabs.Settings;

/// <summary>
/// One rebindable shortcut on the Keyboard card: what it does, what it is bound to now, and whether that
/// is the shipped binding. <see cref="Note"/> carries the answer to the last capture — the clash that
/// refused it, or the default a reset went back to — so the row explains itself where it happened rather
/// than through a dialog.
/// </summary>
public sealed partial class ShortcutRow : ObservableObject {
    public ShortcutRow(ShortcutId id, string description) {
        Id = id;
        Description = description;
    }

    public ShortcutId Id { get; }
    public string Description { get; }

    /// <summary>The keys as they read now — the catalog's own copy for a default, a generated string for
    /// a rebound one.</summary>
    [ObservableProperty] private string _keys = "";

    /// <summary>Whether this is off its shipped binding, which offers the row's reset button.</summary>
    [ObservableProperty] private bool _isCustom;

    /// <summary>A one-line explanation of the last attempt, or empty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    private string _note = "";

    public bool HasNote => Note.Length > 0;
}

/// <summary>The Keyboard card's rows under one scope heading, mirroring how Help groups them.</summary>
public sealed record ShortcutRowGroup(string Title, IReadOnlyList<ShortcutRow> Rows);
