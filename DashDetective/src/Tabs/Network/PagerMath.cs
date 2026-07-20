using System;

namespace DashDetective.Tabs.Network;

/// <summary>
/// Pure paging arithmetic for the connections table, extracted from <see cref="NetworkViewModel"/> so
/// it can be unit-tested without constructing the (timer/sampler-driven) view model. Behaviour is
/// unchanged — these are the exact expressions <c>RebuildPage</c> used inline.
/// </summary>
internal static class PagerMath {
    /// <summary>Total pages for <paramref name="available"/> items at <paramref name="pageSize"/> per
    /// page, never below 1 — an empty list still occupies a single (empty) page.</summary>
    internal static int PageCount(int available, int pageSize) =>
        Math.Max(1, (available + pageSize - 1) / pageSize);

    /// <summary>Clamps a 1-based page into <c>[1, pageCount]</c> (pulls a page back into range if the
    /// list shrank underneath it).</summary>
    internal static int ClampPage(int page, int pageCount) =>
        Math.Clamp(page, 1, pageCount);

    /// <summary>The zero-based index of the first item on a 1-based page.</summary>
    internal static int PageStart(int page, int pageSize) =>
        (page - 1) * pageSize;

    /// <summary>The item count on a page: a full page, the remainder on the last page, or 0 when the
    /// start sits past the end of the list.</summary>
    internal static int SliceCount(int available, int start, int pageSize) =>
        Math.Clamp(available - start, 0, pageSize);
}
