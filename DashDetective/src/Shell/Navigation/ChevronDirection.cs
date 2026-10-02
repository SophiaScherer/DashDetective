namespace DashDetective.Shell.Navigation;

/// <summary>Which way the collapse toggle's caret points. Kept as an enum, separate from the
/// geometry it selects, so the direction rule can be computed and tested without a render backend
/// (<c>Geometry.Parse</c> needs one, which unit tests do not have).</summary>
public enum ChevronDirection {
    Left,
    Right,
    Up,
    Down,
}
