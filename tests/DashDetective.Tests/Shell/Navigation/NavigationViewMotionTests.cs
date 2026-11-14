using Avalonia;
using Avalonia.Media.Transformation;
using DashDetective.Tests.Services.Theming;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace DashDetective.Tests.Shell.Navigation;

/// <summary>Pins the nav rows' hover lift, and that Reduce motion undoes both tween and lift from a later rule in the same file.</summary>
public class NavigationViewMotionTests {
    private const string Content = "StackPanel.navContent";
    private const string Hover = "Button.nav:pointerover " + Content;
    private const string ReduceMotion = ":is(Window).reduceMotion Border.rail " + Content;

    private static readonly List<XElement> Styles = XDocument
        .Load(Path.Combine(PaletteFile.SourceRoot(), "src/Shell/Navigation/NavigationView.axaml"))
        .Descendants()
        .Where(e => e.Name.LocalName == "Style")
        .ToList();

    private static XElement Style(string selector) =>
        Assert.Single(Styles, s => (string?)s.Attribute("Selector") == selector);

    private static XElement? Setter(XElement style, string property) =>
        style.Elements().SingleOrDefault(s => (string?)s.Attribute("Property") == property);

    private static Matrix Transform(string selector) {
        var value = (string?)Setter(Style(selector), "RenderTransform")?.Attribute("Value");
        Assert.NotNull(value);
        return TransformOperations.Parse(value!).Value;
    }

    [Fact]
    public void HoverLift_RisesSlightlyAndOnlyUpward() {
        var lift = Transform(Hover);

        Assert.InRange(lift.M32, -2, -1);
        Assert.Equal(0, lift.M31);
    }

    [Fact]
    public void Content_RestsUnliftedWithAShortTween() {
        Assert.Equal(Matrix.Identity, Transform("Border.rail " + Content));

        var tween = Setter(Style("Border.rail " + Content), "Transitions")!.Descendants()
            .Single(e => e.Name.LocalName == "TransformOperationsTransition");
        Assert.Equal("RenderTransform", (string?)tween.Attribute("Property"));
        Assert.InRange(TimeSpan.Parse((string)tween.Attribute("Duration")!).TotalMilliseconds, 80, 200);
    }

    [Fact]
    public void ReduceMotion_DropsTheTweenAndTheLift() {
        var undo = Style(ReduceMotion);

        var transitions = Setter(undo, "Transitions")!.Elements().Single();
        Assert.Equal("Transitions", transitions.Name.LocalName);
        Assert.False(transitions.HasElements);
        Assert.Equal(Matrix.Identity, Transform(ReduceMotion));
    }

    // Two trigger styles in one file: the later wins while both match, so the undo must come after.
    [Fact]
    public void ReduceMotion_IsDeclaredAfterTheHoverLift() {
        Assert.True(Styles.IndexOf(Style(ReduceMotion)) > Styles.IndexOf(Style(Hover)));
    }
}
