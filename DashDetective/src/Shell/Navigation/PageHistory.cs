using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DashDetective.Shell.Navigation;

/// <summary>
/// The shell's page trail for Back. Every visit pushes the page left behind; stepping back pops it and
/// makes it current without pushing anything, so the visit that follows is recognized as a no-op and
/// Back twice walks two pages back rather than bouncing between two. There is no forward trail.
///
/// Generic so tests can drive it with plain strings; the shell keys it by <see cref="NavItem"/>, whose
/// icon geometry needs a render backend. File Explorer's <c>NavigationHistory</c> is the same idea for
/// folders, kept tab-local.
/// </summary>
internal sealed class PageHistory<T> where T : class {
    /// <summary>How many pages Back can retrace. The oldest is dropped past this.</summary>
    public const int Capacity = 50;

    private readonly List<T> _back = [];

    /// <summary>The page showing now, or null before the first visit.</summary>
    public T? Current { get; private set; }

    /// <summary>Whether there is a page to go back to.</summary>
    public bool CanGoBack => _back.Count > 0;

    /// <summary>The page Back would open, or null at the start of the trail.</summary>
    public T? Previous => _back.Count > 0 ? _back[^1] : null;

    /// <summary>Records that <paramref name="page"/> is now showing. A repeat of the current page records
    /// nothing, which is also what absorbs the visit a step back causes.</summary>
    public void Visit(T page) {
        if (EqualityComparer<T>.Default.Equals(page, Current))
            return;

        if (Current is not null) {
            _back.Add(Current);
            if (_back.Count > Capacity)
                _back.RemoveAt(0);
        }

        Current = page;
    }

    /// <summary>Steps back, handing out the page to open. False, and nothing changes, at the start.</summary>
    public bool TryGoBack([NotNullWhen(true)] out T? target) {
        if (_back.Count == 0) {
            target = null;
            return false;
        }

        target = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        Current = target;
        return true;
    }
}
