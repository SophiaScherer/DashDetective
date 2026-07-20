using Xunit;

namespace DashDetective.Tests;

/// <summary>
/// Proves the test harness is wired up: the project builds against the app assembly, xUnit
/// discovers tests, and <c>dotnet test</c> runs green. Replaced in substance by the per-class
/// suites in later phases; kept as a trivial liveness check.
/// </summary>
public class SmokeTests {
    [Fact]
    public void HarnessRuns() {
        Assert.True(true);
    }
}
