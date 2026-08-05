using DashDetective.Tabs.Toolkit;
using DashDetective.Tests.Fakes;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.Toolkit;

/// <summary>
/// Covers <see cref="ToolkitPaths"/>: which targets the in-app File Explorer can be offered, and that
/// expansion happens at call time. This is what decides whether a row shows one open icon or two.
/// </summary>
public class ToolkitPathsTests {
    /// <summary>"%windir%" is Windows notation; SpecialFolder.Windows is empty elsewhere, so there is
    /// nothing to compare against off Windows. The Unix forms are covered below.</summary>
    [Fact]
    public void Resolve_ExpandsEnvironmentVariables() {
        if (!OperatingSystem.IsWindows())
            return;

        var resolved = ToolkitPaths.Resolve("%windir%");

        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.Windows), resolved,
                     ignoreCase: true);
        Assert.DoesNotContain("%", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_LeavesAPlainPathAlone() {
        Assert.Equal(TestPaths.Of("Temp"), ToolkitPaths.Resolve(TestPaths.Of("Temp")));
    }

    // ----- Unix notation -----
    //
    // Exercised through the Expand(target, windows) seam so both arms run on either dev machine.
    // ExpandEnvironmentVariables only understands %VAR%, so off Windows these forms are all there is.

    [Theory]
    [InlineData("$HOME/Documents")]
    [InlineData("${HOME}/Documents")]
    [InlineData("~/Documents")]
    public void Expand_ResolvesTheUnixFormsToTheHomeFolder(string target) {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("HOME", home);

        Assert.Equal(home + "/Documents", ToolkitPaths.Expand(target, windows: false));
    }

    [Fact]
    public void Expand_ResolvesABareHomeShorthand() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(home, ToolkitPaths.Expand("~", windows: false));
    }

    /// <summary>"~other" names another user's home, which needs a passwd lookup — resolving it against
    /// the current user's would point at the wrong folder, so it stays as typed.</summary>
    [Theory]
    [InlineData("~other/Documents")]
    [InlineData("/etc/~/passwd")]     // only a leading ~ is shorthand
    public void Expand_LeavesANonLeadingOrNamedTildeAlone(string target) {
        Assert.Equal(target, ToolkitPaths.Expand(target, windows: false));
    }

    /// <summary>An unset variable survives as typed, the way "%NOPE%" does on Windows — a row showing
    /// "$NOPE" reads as broken, where an empty string would look like a missing setting.</summary>
    [Theory]
    [InlineData("$DASHDETECTIVE_UNSET/x")]
    [InlineData("${DASHDETECTIVE_UNSET}/x")]
    [InlineData("$")]                 // a bare marker is not a reference
    [InlineData("${HOME")]            // nor is an unclosed brace
    [InlineData("100$ of value")]
    public void Expand_LeavesANonReferenceAlone(string target) {
        Assert.Equal(target, ToolkitPaths.Expand(target, windows: false));
    }

    [Fact]
    public void Expand_ResolvesSeveralReferencesInOneTarget() {
        Environment.SetEnvironmentVariable("DD_ONE", "a");
        Environment.SetEnvironmentVariable("DD_TWO", "b");

        Assert.Equal("/a/b/end", ToolkitPaths.Expand("/$DD_ONE/${DD_TWO}/end", windows: false));
    }

    /// <summary>The Unix forms are inert on Windows, so a "$" in a Windows target is never eaten.</summary>
    [Fact]
    public void Expand_LeavesUnixNotationAloneOnWindows() {
        Environment.SetEnvironmentVariable("DD_ONE", "a");

        Assert.Equal("$DD_ONE", ToolkitPaths.Expand("$DD_ONE", windows: true));
    }

    [Fact]
    public void IsFileSystemPath_TrueForAPlainRootedPath() {
        Assert.True(ToolkitPaths.IsFileSystemPath(TestPaths.Of("Temp")));
    }

    /// <summary>The folder rows are authored unexpanded, so the judgement has to survive expansion —
    /// "%appdata%" has no separator in it until it is resolved.</summary>
    [Theory]
    [InlineData("%appdata%")]
    [InlineData("%windir%")]
    [InlineData(@"%windir%\System32\drivers\etc")]
    public void IsFileSystemPath_TrueForAnUnexpandedWindowsFolder(string target) {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.True(ToolkitPaths.IsFileSystemPath(target));
    }

    /// <summary>Same rule in the other notation: a Linux folder row is authored as "~/…" or "$HOME/…"
    /// and only becomes a rooted path once resolved.</summary>
    [Theory]
    [InlineData("~")]
    [InlineData("~/.config")]
    [InlineData("$HOME/.config")]
    [InlineData("${HOME}/.config")]
    public void IsFileSystemPath_TrueForAnUnexpandedUnixFolder(string target) {
        if (OperatingSystem.IsWindows())
            return;

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
