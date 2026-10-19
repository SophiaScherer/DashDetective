using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using DashDetective.Shell;
using Xunit;

namespace DashDetective.Tests.Shell;

/// <summary>
/// Covers <see cref="BackHint"/>: the toolbar Back tooltip names the page a click opens and the key bound
/// to Back right now, and falls back to a plain "Back" at the start of history.
/// </summary>
public class BackHintTests {
    [Fact]
    public void For_WithAPreviousPage_NamesItAndTheKey() =>
        Assert.Equal("Back to Dashboard (Alt+←)", BackHint.For("Dashboard", new ShortcutBindings()));

    [Fact]
    public void For_AtTheStartOfHistory_SaysBackAndTheKey() =>
        Assert.Equal("Back (Alt+←)", BackHint.For(null, new ShortcutBindings()));

    [Fact]
    public void For_AfterARebind_NamesTheNewKey() {
        var bindings = new ShortcutBindings();
        var gesture = new KeyGesture(Key.Back, KeyModifiers.Alt);
        bindings.TryRebind(ShortcutId.Back, gesture, out _);

        Assert.Equal($"Back to Network ({GestureFormatter.Describe(gesture)})", BackHint.For("Network", bindings));
    }
}
