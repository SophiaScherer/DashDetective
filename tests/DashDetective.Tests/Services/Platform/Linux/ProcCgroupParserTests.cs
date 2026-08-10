using DashDetective.Services.Platform.Linux;
using DashDetective.Tests.Fakes;
using Xunit;

namespace DashDetective.Tests.Services.Platform.Linux;

/// <summary>Covers <see cref="ProcCgroupParser"/>: picking the unified v2 line out of a hybrid file, and
/// the leaf the classifier's scope and service rules match on.</summary>
public class ProcCgroupParserTests {
    private static string Parse(string body) =>
        ProcCgroupParser.Parse(body.Replace("\r\n", "\n").Split('\n'));

    [Fact]
    public void Parse_ReadsTheUnifiedPath() =>
        Assert.Equal(
            "/user.slice/user-1000.slice/user@1000.service/app.slice/app-gnome-firefox-3456.scope",
            Parse(ProcFixtures.ProcCgroupApp));

    /// <summary>The one that matters on a hybrid host: a dozen v1 lines surround the unified one, so
    /// taking the first or last line yields a v1 path. Only hierarchy 0 with an empty controller list
    /// qualifies — and <c>1:name=systemd:</c> is there to prove the empty-list check is doing work.</summary>
    [Fact]
    public void Parse_Hybrid_SkipsEveryV1Hierarchy() =>
        Assert.Equal(
            "/user.slice/user-1000.slice/user@1000.service/app.slice/app-gnome-nautilus-9012.scope",
            Parse(ProcFixtures.ProcCgroupHybrid));

    /// <summary>A v1-only host tells us nothing, so the parser says nothing and the classifier falls
    /// through rather than inventing a category.</summary>
    [Theory]
    [InlineData(ProcFixtures.ProcCgroupV1Only)]
    [InlineData("")]
    [InlineData("not a cgroup line")]
    [InlineData("0:no-second-colon")]
    public void Parse_NoUnifiedLine_IsEmpty(string body) =>
        Assert.Equal("", Parse(body));

    [Theory]
    [InlineData("/user.slice/user-1000.slice/user@1000.service/app.slice/app-gnome-firefox-3456.scope",
                "app-gnome-firefox-3456.scope")]
    [InlineData("/system.slice/cron.service", "cron.service")]
    [InlineData("/", "")]
    [InlineData("", "")]
    public void Leaf_IsTheLastSegment(string path, string expected) =>
        Assert.Equal(expected, ProcCgroupParser.Leaf(path));
}
