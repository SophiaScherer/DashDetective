namespace DashDetective.Services.Diagnostics;

/// <summary>
/// Lets a polling loop log a repeating failure once per streak, not once per tick (single-threaded use).
/// </summary>
internal sealed class FailureStreak {
    private bool _failing;

    /// <summary>Records a failure. True only for the first of an unbroken run, the one worth logging.</summary>
    public bool Failed() {
        var first = !_failing;
        _failing = true;
        return first;
    }

    /// <summary>Records a success, so the next failure starts a new streak.</summary>
    public void Recovered() => _failing = false;
}
