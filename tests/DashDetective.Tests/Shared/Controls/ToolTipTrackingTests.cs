using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using DashDetective.Shared.Controls;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared.Controls;

/// <summary>
/// Covers <see cref="ToolTipTracking"/>: a tip follows the pointer only across its own owner, judged by which
/// control owns the open tip rather than by what is on screen, and the owner's offset always comes back exactly.
/// </summary>
public class ToolTipTrackingTests {
    // card > body > label, beside a second card: the shape of two Dashboard stat cards.
    private readonly Border _card = new();
    private readonly Border _body = new();
    private readonly TextBlock _label = new();
    private readonly Border _otherCard = new();
    private readonly HashSet<Control> _open = [];

    public ToolTipTrackingTests() {
        _body.Child = _label;
        _card.Child = _body;
        var row = new StackPanel();
        row.Children.Add(_card);
        row.Children.Add(_otherCard);
    }

    private Control? OwnerUnder(Control hit) => ToolTipTracking.OpenOwner(hit, _open.Contains);

    [Fact]
    public void OpenOwner_PointerOverADescendant_FindsTheOwnerItIsInside() {
        _open.Add(_card);

        Assert.Same(_card, OwnerUnder(_label));
    }

    [Fact]
    public void OpenOwner_PointerOverTheOwnerItself_FindsIt() {
        _open.Add(_card);

        Assert.Same(_card, OwnerUnder(_card));
    }

    /// <summary>The pointer has left the owner: its tip must not be dragged along to the next card.</summary>
    [Fact]
    public void OpenOwner_PointerOverAnotherCard_FindsNoOwner() {
        _open.Add(_card);

        Assert.Null(OwnerUnder(_otherCard));
    }

    /// <summary>Two cards whose tips read the same are still told apart by owner.</summary>
    [Fact]
    public void OpenOwner_TipHandedToTheNextCard_FollowsTheNewOwnerOnly() {
        _open.Add(_otherCard);

        Assert.Same(_otherCard, OwnerUnder(_otherCard));
        Assert.Null(OwnerUnder(_label));
    }

    /// <summary>Nested owners: the innermost open tip is the one under the pointer, as the framework picks it.</summary>
    [Fact]
    public void OpenOwner_NestedOpenOwners_FindsTheInnermost() {
        _open.Add(_card);
        _open.Add(_body);

        Assert.Same(_body, OwnerUnder(_label));
    }

    [Fact]
    public void OpenOwner_NothingOpen_FindsNoOwner() => Assert.Null(OwnerUnder(_label));

    [Fact]
    public void OpenOwner_NoHit_FindsNoOwner() => Assert.Null(ToolTipTracking.OpenOwner(null, _open.Contains));

    /// <summary>The pointer over the tip's own window still belongs to the card underneath it.</summary>
    [Fact]
    public void TipOwner_PointerOverATip_FindsTheControlItBelongsTo() {
        var tip = new ToolTip();
        _ = new Popup { Child = tip, PlacementTarget = _card };

        Assert.Same(_card, ToolTipTracking.TipOwner(tip));
    }

    /// <summary>A tip's text sits under its template, so the owner is found from anywhere inside it.</summary>
    [Fact]
    public void TipOwner_PointerOverTheTipsText_FindsTheControlItBelongsTo() {
        var text = new TextBlock();
        var tip = new ToolTip { Template = new FuncControlTemplate<ToolTip>((_, _) => new Border { Child = text }) };
        _ = new Popup { Child = tip, PlacementTarget = _card };
        tip.ApplyTemplate();

        Assert.Same(_card, ToolTipTracking.TipOwner(text));
    }

    [Fact]
    public void TipOwner_PointerOverAPage_FindsNoTip() => Assert.Null(ToolTipTracking.TipOwner(_label));

    [Fact]
    public void Next_PointerAtTheAnchor_StepsANudgeAsideSoTheTipIsReplaced() =>
        Assert.Equal(new Vector(ToolTipTracking.Nudge, 20), ToolTipTracking.Next(new Vector(0, 20), new Vector(0, 20), default));

    /// <summary>Every move changes the offsets, which is what makes Avalonia re-place the tip at the cursor.</summary>
    [Fact]
    public void Next_RepeatedMoves_AlternateBetweenTheOwnersOffsetAndTheNudge() {
        var baseline = new Vector(0, 20);
        var current = baseline;
        for (var move = 0; move < 5; move++) {
            var next = ToolTipTracking.Next(current, baseline, default);
            Assert.NotEqual(current, next);
            current = next;
        }
    }

    /// <summary>Back to the owner's own offset bit for bit, not to a sum that rounds away from it.</summary>
    [Fact]
    public void Next_AfterANudge_ReturnsTheOwnersOffsetExactly() {
        var baseline = new Vector(0.1, 20);
        var nudged = ToolTipTracking.Next(baseline, baseline, default);

        Assert.Equal(baseline, ToolTipTracking.Next(nudged, baseline, default));
    }

    /// <summary>Over the tip, the tip moves by exactly how far the pointer is from where it was anchored.</summary>
    [Fact]
    public void Next_PointerShiftedFromTheAnchor_TranslatesTheTipByTheShift() =>
        Assert.Equal(new Vector(12, 52), ToolTipTracking.Next(new Vector(0, 20), new Vector(0, 20), new Vector(12, 32)));

    /// <summary>The nudge never shows: it stays under a device pixel even at 300% scaling.</summary>
    [Fact]
    public void Nudge_StaysUnderAPixelAtTheHighestScale() => Assert.True(ToolTipTracking.Nudge * 3 < 0.5);
}
