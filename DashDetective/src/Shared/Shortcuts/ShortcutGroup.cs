using System.Collections.Generic;

namespace DashDetective.Shared.Shortcuts;

/// <summary>One headed block of shortcuts in the Help modal — the shortcuts of a single
/// <see cref="ShortcutScope"/>, under a reader-facing title.</summary>
/// <param name="Title">The heading, e.g. "File Explorer".</param>
/// <param name="Shortcuts">The listed shortcuts, in catalog order.</param>
public sealed record ShortcutGroup(string Title, IReadOnlyList<Shortcut> Shortcuts);
