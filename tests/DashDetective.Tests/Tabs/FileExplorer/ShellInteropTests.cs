using DashDetective.Tabs.FileExplorer;
using System;
using Xunit;

namespace DashDetective.Tests.Tabs.FileExplorer;

/// <summary>Covers the <see cref="IShellInterop"/> seam: which implementation the platform resolves to,
/// and the fallback contract — notably that <c>Open</c> stays live on an unsupported host, because it was
/// never platform-guarded in the first place.
///
/// <see cref="FileExplorerViewModel"/> itself is not covered: it reaches <c>FileTypeCatalog</c>, whose
/// static initialiser calls <c>Geometry.Parse</c> and needs a render backend these tests deliberately
/// don't have (see the Testing conventions in AGENTS.md).</summary>
public class ShellInteropTests {
    [Fact]
    public void ForCurrentPlatform_ResolvesTheShellForThisHost() {
        var shell = IShellInterop.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsShellInterop>(shell);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxShellInterop>(shell);
        else
            Assert.IsType<UnsupportedShellInterop>(shell);
    }

    /// <summary>Without a Win32 shell, the type name is derived from the extension — the same fallback
    /// the Windows reader uses when <c>SHGetFileInfo</c> reports nothing.</summary>
    [Theory]
    [InlineData("config.json", false, "JSON File")]
    [InlineData("notes.TXT", false, "TXT File")]
    [InlineData("README", false, "File")]
    [InlineData(@"C:\Windows", true, "File folder")]
    public void Unsupported_GetTypeName_FallsBackToTheExtensionLabel(
        string path, bool isDirectory, string expected) =>
        Assert.Equal(expected, new UnsupportedShellInterop().GetTypeName(path, isDirectory));

    /// <summary>Properties needs a native dialog, so it does nothing — but it must not throw, since the
    /// code-behind calls it unconditionally.</summary>
    [Fact]
    public void Unsupported_ShowProperties_DoesNothingWithoutThrowing() =>
        new UnsupportedShellInterop().ShowProperties(IntPtr.Zero, @"C:\Windows");

    /// <summary>The Windows reader shares the same extension fallback, so a path the shell can't describe
    /// still yields a sensible label rather than an empty cell.</summary>
    [Fact]
    public void Windows_GetTypeName_AlwaysReturnsSomething() {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.False(string.IsNullOrWhiteSpace(
            new WindowsShellInterop().GetTypeName(@"C:\definitely-not-a-real-file.qzx", false)));
    }
}
