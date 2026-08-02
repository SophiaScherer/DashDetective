using DashDetective.Tabs.Toolkit;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="ToolkitPaths"/>: which targets the in-app File Explorer can be offered, and that
/// expansion happens at call time. This is what decides whether a row shows one open icon or two.
/// </summary>
public class ToolkitPathsTests {
    [Fact]
    public void Resolve_ExpandsEnvironmentVariables() {
        var resolved = ToolkitPaths.Resolve("%windir%");

        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.Windows), resolved,
                     ignoreCase: true);
        Assert.DoesNotContain("%", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_LeavesAPlainPathAlone() {
        Assert.Equal(@"C:\Temp", ToolkitPaths.Resolve(@"C:\Temp"));
    }

    /// <summary>The folder rows are authored unexpanded, so the judgement has to survive expansion —
    /// "%appdata%" has no separator in it until it is resolved.</summary>
    [Theory]
    [InlineData("%appdata%")]
    [InlineData("%windir%")]
    [InlineData(@"%windir%\System32\drivers\etc")]
    [InlineData(@"C:\Temp")]
    public void IsFileSystemPath_TrueForSomewhereOnDisk(string target) {
        Assert.True(ToolkitPaths.IsFileSystemPath(target));
    }

    /// <summary>A shell location resolves through the shell namespace, not the filesystem: Explorer
    /// opens it, the in-app File Explorer has nothing to navigate to.</summary>
    [Theory]
    [InlineData("shell:startup")]
    [InlineData("SHELL:Downloads")]
    [InlineData("regedit")]
    [InlineData("ipconfig")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsFileSystemPath_FalseForAnythingElse(string? target) {
        Assert.False(ToolkitPaths.IsFileSystemPath(target));
    }
}
