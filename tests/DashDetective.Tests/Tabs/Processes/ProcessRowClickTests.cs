using Avalonia.Input;
using DashDetective.Tabs.Processes;
using Xunit;

namespace DashDetective.Tests.Tabs.Processes;

/// <summary>Pins <see cref="ProcessRowClick.ShouldDeselect"/>: only a plain single left click on the
/// row that is the whole selection clears it. Everything else keeps its existing meaning.</summary>
public class ProcessRowClickTests {
    [Fact]
    public void ShouldDeselect_PlainSingleLeftClickOnTheOnlySelection_IsTrue() =>
        Assert.True(ProcessRowClick.ShouldDeselect(true, KeyModifiers.None, 1, MouseButton.Left));

    [Fact]
    public void ShouldDeselect_RowNotTheOnlySelection_IsFalse() =>
        Assert.False(ProcessRowClick.ShouldDeselect(false, KeyModifiers.None, 1, MouseButton.Left));

    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Meta)]
    public void ShouldDeselect_AnyModifier_IsFalse(KeyModifiers modifiers) =>
        Assert.False(ProcessRowClick.ShouldDeselect(true, modifiers, 1, MouseButton.Left));

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ShouldDeselect_DoubleOrLaterClick_IsFalse(int clickCount) =>
        Assert.False(ProcessRowClick.ShouldDeselect(true, KeyModifiers.None, clickCount, MouseButton.Left));

    [Theory]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    [InlineData(MouseButton.None)]
    public void ShouldDeselect_NonLeftButton_IsFalse(MouseButton button) =>
        Assert.False(ProcessRowClick.ShouldDeselect(true, KeyModifiers.None, 1, button));
}
