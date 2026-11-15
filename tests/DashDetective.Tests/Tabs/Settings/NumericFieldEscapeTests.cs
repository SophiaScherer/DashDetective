using DashDetective.Tests.Services.Theming;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Tabs.Settings;

/// <summary>Pins that <c>NumericField</c> hears Escape on the tunnel with handled events, since the shell handles it first; pinned in source for lack of a headless harness.</summary>
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
