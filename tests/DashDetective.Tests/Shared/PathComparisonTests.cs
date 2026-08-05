using DashDetective.Shared;
using System;
using System.Collections.Generic;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="PathComparison"/>: path identity folds case only where the filesystem
/// does, and the comparison and the comparer never disagree about it.</summary>
public class PathComparisonTests {
    [Fact]
    public void Comparison_FoldsCaseOnlyOnWindows() {
        var expected = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Assert.Equal(expected, PathComparison.Comparison);
    }

    [Fact]
    public void Comparer_AgreesWithTheComparison() {
        // A set keyed by the comparer and an Equals using the comparison must not disagree, or a path
        // would dedupe one way and compare the other.
        var equal = string.Equals("/home/Sophia", "/home/sophia", PathComparison.Comparison);

        Assert.Equal(equal, PathComparison.Comparer.Equals("/home/Sophia", "/home/sophia"));
    }

    [Fact]
    public void Comparer_KeepsCaseVariantsApartOffWindows() {
        var set = new HashSet<string>(PathComparison.Comparer) { "/home/Sophia", "/home/sophia" };

        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 2, set.Count);
    }
}
