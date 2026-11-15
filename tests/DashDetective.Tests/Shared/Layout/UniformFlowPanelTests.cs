using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using DashDetective.Shared.Layout;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="UniformFlowPanel"/> as a strip of button cards: a grip on the button is a handle, and the strip reorders by drag and keyboard.</summary>
public class UniformFlowPanelTests {
    private const double Width = 1000;

    private static (UniformFlowPanel Panel, List<Button> Links) Strip(ReorderGrip grip, bool markGrip = true) {
        var panel = new UniformFlowPanel {
            MinItemWidth = 150, ColumnSpacing = 12, RowSpacing = 12, DragGrip = grip, ConfineDrag = true,
        };
        var links = new List<Button>();
        foreach (var id in new[] { "a", "b", "c" }) {
            var link = new Button { Height = 80 };
            Reorder.SetIsGrip(link, markGrip);
            var card = new Border { Child = link };
            Reorder.SetId(card, id);
            panel.Children.Add(card);
            links.Add(link);
        }
        Layout(panel);
        return (panel, links);
    }

    private static void Layout(UniformFlowPanel panel) {
        panel.InvalidateMeasure();
        panel.Measure(new Size(Width, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, Width, panel.DesiredSize.Height));
    }

    /// <summary>A button-rooted item is reached through the button, not also through its presenter.</summary>
    [Fact]
    public void Layout_AGeneratedButton_LeavesItsPresenterOutOfTheTabOrder() {
        var panel = new UniformFlowPanel { MinItemWidth = 150, DragGrip = ReorderGrip.Marked };
        var presenter = new ContentPresenter { Content = new Button { Height = 80 } };
        panel.Children.Add(presenter);
        presenter.UpdateChild();    // what the item generator does; nothing headless does it otherwise
        Layout(panel);

        Assert.IsType<Button>(presenter.Child);
        Assert.False(presenter.Focusable);
    }

    /// <summary>The presenter gains its child during measure, so the tab stop is decided at arrange.</summary>
    [Fact]
    public void Arrange_AButtonThePresenterGainedDuringMeasure_LeavesThePresenterOut() {
        var panel = new UniformFlowPanel { MinItemWidth = 150, DragGrip = ReorderGrip.Marked };
        var presenter = new ContentPresenter { Content = new Button { Height = 80 } };
        panel.Children.Add(presenter);
        panel.Measure(new Size(Width, double.PositiveInfinity));
        presenter.UpdateChild();

        panel.Arrange(new Rect(0, 0, Width, 80));

        Assert.False(presenter.Focusable);
    }

    /// <summary>A plain-surface card has nothing else to focus, so its presenter is the stop.</summary>
    [Fact]
    public void Layout_AGeneratedSurface_MakesItsPresenterATabStop() {
        var panel = new UniformFlowPanel { MinItemWidth = 150, DragGrip = ReorderGrip.Item };
        var presenter = new ContentPresenter { Content = new Border { Height = 80 } };
        panel.Children.Add(presenter);
        presenter.UpdateChild();    // what the item generator does; nothing headless does it otherwise
        Layout(panel);

        Assert.IsType<Border>(presenter.Child);
        Assert.True(presenter.Focusable);
    }

    [Fact]
    public void TryGetHandle_OnAMarkedButtonInsideACard_IsTheCard() {
        var (panel, links) = Strip(ReorderGrip.Marked);

        Assert.True(panel.TryGetHandle(links[1], out var handle));
        Assert.Same(panel.Children[1], handle.Item);
        Assert.Same(links[1], handle.Grip);
    }

    /// <summary>Under <see cref="ReorderGrip.Item"/> a press on a button belongs to the button, so the links are marked instead.</summary>
    [Fact]
    public void TryGetHandle_OnAButtonUnderItem_IsNotAHandle() {
        var (panel, links) = Strip(ReorderGrip.Item, markGrip: false);

        Assert.False(panel.TryGetHandle(links[1], out _));
    }

    [Fact]
    public void Drag_AlongTheStrip_CommitsTheNewOrder() {
        var (panel, _) = Strip(ReorderGrip.Marked);
        IReadOnlyList<string>? committed = null;
        panel.OrderChanged += ids => committed = ids;
        var first = panel.Children[0];
        var third = panel.Children[2];

        panel.Drag.Press(new ReorderHandle(first, first, first), first.Bounds.Center);
        panel.Drag.MoveTo(third.Bounds.Center, null);
        Layout(panel);
        panel.Drag.Release(null);

        Assert.Equal(new[] { "b", "c", "a" }, committed);
    }

    [Fact]
    public void TryMoveFocused_OnACard_MovesItWithinTheStrip() {
        var (panel, links) = Strip(ReorderGrip.Marked);
        IReadOnlyList<string>? committed = null;
        panel.OrderChanged += ids => committed = ids;

        Assert.True(panel.TryMoveFocused(links[0], 1));
        Assert.Equal(new[] { "b", "a", "c" }, committed);
    }
}
