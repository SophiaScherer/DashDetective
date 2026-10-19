using DashDetective.Shared.Shortcuts;
using System.Collections.Generic;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IShortcutTarget"/>: records every id it is offered and claims only the ones it
/// was told to, so a test can stage a page that has, or has not, somewhere to step to.
/// </summary>
internal sealed class FakeShortcutTarget(params ShortcutId[] claims) : IShortcutTarget {
    private readonly HashSet<ShortcutId> _claims = [.. claims];

    /// <summary>Every id offered, in order.</summary>
    public List<ShortcutId> Offered { get; } = [];

    public ShortcutScope Scope => ShortcutScope.FileExplorer;

    public bool HandleShortcut(ShortcutId id) {
        Offered.Add(id);
        return _claims.Contains(id);
    }
}
