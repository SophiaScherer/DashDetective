using DashDetective.Shared.Layout;
using System;
using Xunit;

namespace DashDetective.Tests.Shared.Layout;

/// <summary>Covers <see cref="ReorderSlides"/>: an item eases into its slot, and a second move mid-slide continues from where it is drawn.</summary>
public class ReorderSlidesTests {
    private static readonly TimeSpan Half = ReorderSlides.Duration / 2;

    [Fact]
    public void Ease_RunsFromZeroToOneAndOnlyRises() {
        Assert.Equal(0, ReorderSlides.Ease(0));
        Assert.Equal(1, ReorderSlides.Ease(1));

        var previous = 0.0;
        for (var t = 0.05; t <= 1; t += 0.05) {
            var eased = ReorderSlides.Ease(t);
            Assert.True(eased > previous);
            previous = eased;
        }
    }

    /// <summary>Ease-out: more than half the distance is covered in the first half of the time.</summary>
    [Fact]
    public void Ease_Halfway_IsPastHalf() => Assert.True(ReorderSlides.Ease(0.5) > 0.5);

    [Fact]
    public void OffsetAt_Start_IsTheWholeDistance() {
        var slides = new ReorderSlides();
        var item = new object();

        slides.Start(item, 300, -40, TimeSpan.Zero);

        Assert.Equal((300.0, -40.0), slides.OffsetAt(item, TimeSpan.Zero));
    }

    [Fact]
    public void OffsetAt_Halfway_IsBetweenTheEnds() {
        var slides = new ReorderSlides();
        var item = new object();

        slides.Start(item, 300, 0, TimeSpan.Zero);
        var (x, _) = slides.OffsetAt(item, Half);

        Assert.InRange(x, 1, 149);
    }

    [Fact]
    public void OffsetAt_AfterTheDuration_IsZero() {
        var slides = new ReorderSlides();
        var item = new object();

        slides.Start(item, 300, 120, TimeSpan.Zero);

        Assert.Equal((0.0, 0.0), slides.OffsetAt(item, ReorderSlides.Duration));
    }

    [Fact]
    public void OffsetAt_AnItemNotSliding_IsZero() =>
        Assert.Equal((0.0, 0.0), new ReorderSlides().OffsetAt(new object(), TimeSpan.Zero));

    /// <summary>A re-targeted item is drawn where it was a moment before, plus the distance its slot moved.</summary>
    [Fact]
    public void Start_MidSlide_ContinuesFromWhereTheItemIsDrawn() {
        var slides = new ReorderSlides();
        var item = new object();
        slides.Start(item, 300, 0, TimeSpan.Zero);
        var (drawn, _) = slides.OffsetAt(item, Half);

        slides.Start(item, -100, 0, Half);

        Assert.Equal(drawn - 100, slides.OffsetAt(item, Half).X, 6);
    }

    [Fact]
    public void Finish_DropsOnlyTheSlidesThatArrived() {
        var slides = new ReorderSlides();
        var early = new object();
        var late = new object();
        slides.Start(early, 10, 0, TimeSpan.Zero);
        slides.Start(late, 10, 0, Half);

        var done = slides.Finish(ReorderSlides.Duration);

        Assert.Equal([early], done);
        Assert.False(slides.IsSliding(early));
        Assert.True(slides.IsSliding(late));
    }

    [Fact]
    public void Stop_EndsTheSlideAtOnce() {
        var slides = new ReorderSlides();
        var item = new object();
        slides.Start(item, 10, 0, TimeSpan.Zero);

        slides.Stop(item);

        Assert.Equal(0, slides.Count);
        Assert.Equal((0.0, 0.0), slides.OffsetAt(item, TimeSpan.Zero));
    }
}
