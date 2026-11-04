using DashDetective.Tests.Services.Theming;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Pins how <c>NumericField</c> hears Escape. The shell's tunnelling Escape handler dismisses a
/// banner and marks the key handled before it reaches the box, so a bubbling <c>KeyDown</c> never saw it
/// and the half-typed number stayed. The box has no headless harness, so the registration is pinned in
/// source.</summary>
public class NumericFieldEscapeTests {
    private static string Source() =>
        File.ReadAllText(Path.Combine(PaletteFile.SourceRoot(), "src/Tabs/Settings/NumericField.axaml.cs"));

    [Fact]
    public void Constructor_EscapeIsHeardTunnelledEvenWhenHandled() =>
        Assert.Contains("Entry.AddHandler(KeyDownEvent, OnEscape, RoutingStrategies.Tunnel, handledEventsToo: true)",
                        Source());

    [Fact]
    public void OnKeyDown_NoLongerHandlesEscapeOnTheBubble() =>
        Assert.DoesNotContain("e.Key == Key.Escape", Source().Split("private void OnEscape")[0]);
}
