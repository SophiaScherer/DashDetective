using System;
using System.Collections.Generic;

namespace DashDetective.Shared.Layout;

/// <summary>
/// The motion behind a reorder: each moved item eases from where it was drawn to its new slot, as an
/// offset the panel applies as a render transform so layout never sees it. No Avalonia types, so the
/// easing and the hand-off between two moves test without a render loop.
/// </summary>
internal sealed class ReorderSlides {
    /// <summary>How long one slide takes: short enough that nothing waits on it.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(180);

    private readonly Dictionary<object, Slide> _slides = new();

    public int Count => _slides.Count;

    public IReadOnlyCollection<object> Items => _slides.Keys;

    public bool IsSliding(object item) => _slides.ContainsKey(item);

    /// <summary>Starts a slide for an item drawn (dx, dy) away from its new slot. One already moving
    /// carries its current offset in, so a second move mid-slide continues from where it is drawn.</summary>
    public void Start(object item, double dx, double dy, TimeSpan now) {
        var (x, y) = OffsetAt(item, now);
        _slides[item] = new Slide(dx + x, dy + y, now);
    }

    /// <summary>How far from its slot the item is drawn now; zero once it has arrived.</summary>
    public (double X, double Y) OffsetAt(object item, TimeSpan now) {
        if (!_slides.TryGetValue(item, out var slide))
            return (0, 0);

        var remaining = 1 - Ease(Progress(slide.Start, now));
        return (slide.FromX * remaining, slide.FromY * remaining);
    }

    public void Stop(object item) => _slides.Remove(item);

    /// <summary>Drops every slide that has arrived and hands those items back, so the panel can clear
    /// what it drew them with.</summary>
    public List<object> Finish(TimeSpan now) {
        var done = new List<object>();
        foreach (var (item, slide) in _slides)
            if (Progress(slide.Start, now) >= 1)
                done.Add(item);
        foreach (var item in done)
            _slides.Remove(item);
        return done;
    }

    /// <summary>Ease-out cubic: fast off the mark, gentle into the slot.</summary>
    internal static double Ease(double t) => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3);

    private static double Progress(TimeSpan start, TimeSpan now) =>
        Math.Clamp((now - start) / Duration, 0, 1);

    private readonly record struct Slide(double FromX, double FromY, TimeSpan Start);
}
