using Avalonia.Input;
using DashDetective.Shared.Shortcuts;
using Xunit;

namespace DashDetective.Tests.Shared.Shortcuts;

/// <summary>Pins that the reorder gesture resolves, does not collide with the Ctrl+arrow pager, and spares a text box's word selection.</summary>
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

    /// <summary>Ctrl+Shift+arrow selects by word in a text box, so a field inside a widget keeps it.</summary>
    [Theory]
    [InlineData(Key.Left, ShortcutScope.Global)]
    [InlineData(Key.Right, ShortcutScope.Global)]
    [InlineData(Key.Left, ShortcutScope.Network)]
    [InlineData(Key.Right, ShortcutScope.Network)]
    public void CtrlShiftArrow_TextInputFocused_LeavesTheKeyToTheTextBox(Key key, ShortcutScope scope) {
        var bindings = new ShortcutBindings();

        var ids = bindings.Resolve(key, KeyModifiers.Control | KeyModifiers.Shift, true, scope);

        Assert.DoesNotContain(ShortcutId.MoveItemBack, ids);
        Assert.DoesNotContain(ShortcutId.MoveItemForward, ids);
    }

    /// <summary>The guard belongs to the action, not to the default gesture, so a rebind keeps it.</summary>
    [Fact]
    public void Rebind_KeepsTheMoveOutOfTextInput() {
        var bindings = new ShortcutBindings();
        var gesture = new KeyGesture(Key.M, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.True(bindings.TryRebind(ShortcutId.MoveItemBack, gesture, out _));

        Assert.Empty(bindings.Resolve(gesture.Key, gesture.KeyModifiers, true, ShortcutScope.Global));
        Assert.Contains(ShortcutId.MoveItemBack,
                        bindings.Resolve(gesture.Key, gesture.KeyModifiers, false, ShortcutScope.Global));
    }
}
