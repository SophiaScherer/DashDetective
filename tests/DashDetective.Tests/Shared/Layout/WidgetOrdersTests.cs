using DashDetective.Shared.Layout;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="WidgetOrders"/>: the round-trip, a total decode of anything malformed,
/// and what a saved layout does when the page's widgets have since changed.</summary>
public class WidgetOrdersTests {
    private static Dictionary<string, IReadOnlyList<string>> Orders(
        params (string Page, string[] Ids)[] pages) {
        var map = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var (page, ids) in pages)
            map[page] = ids;
        return map;
    }

    // ===== Round-trip =====

    [Fact]
    public void RoundTrips() {
        var orders = Orders(
            ("network", ["network.ipconfig", "network.adapters", "network.throughput"]),
            ("storage", ["storage.activity", "storage.partitions"]));

        var back = WidgetOrders.Decode(WidgetOrders.Encode(orders));

        Assert.Equal(2, back.Count);
        Assert.Equal(orders["network"], back["network"]);
        Assert.Equal(orders["storage"], back["storage"]);
    }

    [Fact]
    public void Encode_SkipsBlanksAndDuplicates() {
        var encoded = WidgetOrders.Encode(Orders(("net", ["a", "", "a", "b"])));
        Assert.Equal(["a", "b"], WidgetOrders.Decode(encoded)["net"]);
    }

    [Fact]
    public void Encode_SkipsAPageWithNoWidgets() {
        Assert.Equal("", WidgetOrders.Encode(Orders(("net", []))));
    }

    // ===== Decode is total =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage-with-no-separators")]
    [InlineData("\u001E")]
    [InlineData("\u001F")]
    [InlineData("page-with-no-ids")]
    [InlineData("\u001Fleading-separator")]
    public void Decode_BadInput_IsEmptyRatherThanThrowing(string? encoded) {
        Assert.Empty(WidgetOrders.Decode(encoded));
    }

    [Fact]
    public void Decode_DropsOnlyTheBadRecord() {
        // A page key with no ids after it is malformed; the record beside it still reads.
        var encoded = "broken\u001Egood\u001Fgood.one";
        var back = WidgetOrders.Decode(encoded);
        Assert.Single(back);
        Assert.Equal(["good.one"], back["good"]);
    }

    // ===== Resolve =====

    private static readonly string[] Declared = ["a", "b", "c", "d"];

    [Fact]
    public void Resolve_NoSavedOrder_IsTheDeclaredOne() {
        Assert.Equal(Declared, WidgetOrders.Resolve(Declared, []));
    }

    [Fact]
    public void Resolve_FullSavedOrder_Wins() {
        Assert.Equal(["d", "c", "b", "a"], WidgetOrders.Resolve(Declared, ["d", "c", "b", "a"]));
    }

    [Fact]
    public void Resolve_UnknownSavedId_IsDropped() {
        // "z" was removed from the page in this build; it takes no slot.
        Assert.Equal(["d", "a", "b", "c"], WidgetOrders.Resolve(Declared, ["d", "z", "a", "b", "c"]));
    }

    [Fact]
    public void Resolve_NewWidget_LandsBesideItsDeclaredNeighbour_NotAtTheEnd() {
        // "c" was added in this build. The save names a, b, d — c must stay after b, where its author
        // put it, rather than being appended below a layout the user arranged once.
        Assert.Equal(["a", "b", "c", "d"], WidgetOrders.Resolve(Declared, ["a", "b", "d"]));
    }

    [Fact]
    public void Resolve_NewWidgetFirst_StaysFirst() {
        Assert.Equal(["a", "b", "c", "d"], WidgetOrders.Resolve(Declared, ["b", "c", "d"]));
    }

    [Fact]
    public void Resolve_NewWidget_FollowsAReorderedNeighbour() {
        // The user moved d to the front; c is new and declared after b, so it follows b there.
        Assert.Equal(["d", "a", "b", "c"], WidgetOrders.Resolve(Declared, ["d", "a", "b"]));
    }

    [Fact]
    public void Resolve_DuplicateSavedId_FirstOccurrenceWins() {
        Assert.Equal(["c", "a", "b", "d"], WidgetOrders.Resolve(Declared, ["c", "a", "c", "b", "d"]));
    }

    [Fact]
    public void Resolve_DuplicateDeclaredId_IsListedOnce() {
        Assert.Equal(["b", "a"], WidgetOrders.Resolve(["a", "b", "a"], ["b", "a"]));
    }

    [Fact]
    public void Resolve_RenamedId_ReappearsAtItsDeclaredPosition() {
        // A rename is indistinguishable from remove + add, so it costs that widget's saved position
        // and nothing else: "b" was renamed, and the other three keep the user's order.
        Assert.Equal(["d", "a", "b", "c"], WidgetOrders.Resolve(Declared, ["d", "a", "old-b", "c"]));
    }

    [Fact]
    public void Resolve_SaveFromALaterBuild_IgnoresEveryUnknownId() {
        Assert.Equal(Declared, WidgetOrders.Resolve(Declared, ["x", "y", "z"]));
    }

    [Fact]
    public void Resolve_NoDeclaredWidgets_IsEmpty() {
        Assert.Empty(WidgetOrders.Resolve([], ["a"]));
    }
}
