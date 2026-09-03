using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using DashDetective.Shared.Layout;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="Reorder.OwnsItsOwnGesture"/>: the one list of controls that take clicks
/// of their own, which a drag and a widget header's double-tap both refuse to act on. Constructing the
/// controls needs no render pass — nothing here is measured or templated.</summary>
public class ReorderTests {
    [Fact]
    public void OwnsItsOwnGesture_Button_IsRefused() =>
        Assert.True(Reorder.OwnsItsOwnGesture(new Button()));

    [Fact]
    public void OwnsItsOwnGesture_ToggleButton_IsRefused() =>
        Assert.True(Reorder.OwnsItsOwnGesture(new ToggleButton()));

    [Fact]
    public void OwnsItsOwnGesture_TextBox_IsRefused() =>
        Assert.True(Reorder.OwnsItsOwnGesture(new TextBox()));

    [Fact]
    public void OwnsItsOwnGesture_ComboBox_IsRefused() =>
        Assert.True(Reorder.OwnsItsOwnGesture(new ComboBox()));

    [Fact]
    public void OwnsItsOwnGesture_ScrollBar_IsRefused() =>
        Assert.True(Reorder.OwnsItsOwnGesture(new ScrollBar()));

    /// <summary>The surfaces a header is actually made of, which must stay draggable and double-tappable.
    /// A caption that counted as its own gesture would leave the affordance with nothing to grab.</summary>
    [Theory]
    [MemberData(nameof(PlainControls))]
    public void OwnsItsOwnGesture_PlainControl_IsAllowed(Control control) =>
        Assert.False(Reorder.OwnsItsOwnGesture(control));

    public static TheoryData<Control> PlainControls => [
        new Border(),
        new TextBlock(),
        new StackPanel(),
        new ContentPresenter(),
    ];
}
