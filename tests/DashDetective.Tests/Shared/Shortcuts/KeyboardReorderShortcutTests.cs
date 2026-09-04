using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using Xunit;

namespace DashDetective.Tests.Shared.Shortcuts;

/// <summary>Pins that the keyboard reorder gesture resolves, and that it does not collide with the
/// Ctrl+arrow pager it sits beside.</summary>
public class KeyboardReorderShortcutTests {
    [Theory]
    [InlineData(Key.Right, ShortcutId.MoveItemForward)]
    [InlineData(Key.Left, ShortcutId.MoveItemBack)]
    public void CtrlShiftArrow_ResolvesToTheMove(Key key, ShortcutId expected) {
        var bindings = new ShortcutBindings();

        var resolved = bindings.TryResolve(
            key, KeyModifiers.Control | KeyModifiers.Shift, false, ShortcutScope.Global, out var id);

        Assert.True(resolved, $"{key} with Ctrl+Shift resolved to nothing.");
        Assert.Equal(expected, id);
    }

    /// <summary>Ctrl+arrow alone is the Network pager, so the extra Shift has to be what tells them
    /// apart rather than the arrow being claimed twice.</summary>
    [Fact]
    public void CtrlArrow_StillPagesOnNetwork() {
        var bindings = new ShortcutBindings();

        Assert.True(bindings.TryResolve(
            Key.Right, KeyModifiers.Control, false, ShortcutScope.Network, out var id));
        Assert.Equal(ShortcutId.NextPage, id);
    }
}
