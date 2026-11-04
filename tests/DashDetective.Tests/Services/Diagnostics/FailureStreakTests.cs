using DashDetective.Services.Diagnostics;
using Xunit;

namespace DashDetective.Tests.Services.Diagnostics;

/// <summary>Pins that a repeating failure is reported once per unbroken run and again after a success.</summary>
public class FailureStreakTests {
    [Fact]
    public void Failed_RepeatedWithoutRecovery_ReportsOnlyTheFirst() {
        var streak = new FailureStreak();

        Assert.True(streak.Failed());
        Assert.False(streak.Failed());
        Assert.False(streak.Failed());
    }

    [Fact]
    public void Failed_AfterRecovered_ReportsAgain() {
        var streak = new FailureStreak();
        streak.Failed();

        streak.Recovered();

        Assert.True(streak.Failed());
    }
}
