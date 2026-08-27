namespace DashDetective.Shared;

/// <summary>Shared pointer-drag constants. Promoted from NavigationView once the widget board wanted
/// the same threshold.</summary>
public static class PointerDrag {
    /// <summary>Movement before a press counts as a drag, so a click never nudges anything.</summary>
    public const double Threshold = 6;
}
