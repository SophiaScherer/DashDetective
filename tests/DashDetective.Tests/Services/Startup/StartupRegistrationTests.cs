using DashDetective.Services.Startup;
using System;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Services.Startup;

/// <summary>
/// Covers the <see cref="IStartupRegistration"/> seam: which registration the platform resolves to, the
/// <c>.desktop</c> body the Linux arm writes, and the round trip through a real directory.
///
/// The Linux arm is portable <c>System.IO</c> over an injected directory, so the whole round trip runs
/// on a Windows dev machine — only the *default* directory is Linux-shaped, and that is not what these
/// exercise.
/// </summary>
public class StartupRegistrationTests : IDisposable {
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "dd-autostart-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        try {
            Directory.Delete(_directory, recursive: true);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // A leftover temp folder is not worth failing a test over.
        }
        GC.SuppressFinalize(this);
    }

    private LinuxStartupRegistration Registration() => new(_directory);

    private string EntryPath => Path.Combine(_directory, "DashDetective.desktop");

    [Fact]
    public void ForCurrentPlatform_ResolvesTheRegistrationForThisHost() {
        var registration = IStartupRegistration.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsStartupRegistration>(registration);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxStartupRegistration>(registration);
        else
            Assert.IsType<UnsupportedStartupRegistration>(registration);
    }

    [Fact]
    public void Unsupported_ReportsNotEnabledAndIgnoresWrites() {
        var registration = new UnsupportedStartupRegistration();

        registration.SetEnabled(true);

        Assert.False(registration.IsEnabled());
    }

    /// <summary>The whole point of the milestone: the toggle has to create and remove a real file, and
    /// report its own state back from disk rather than from memory.</summary>
    [Fact]
    public void Linux_SetEnabled_WritesThenRemovesTheAutostartEntry() {
        var registration = Registration();

        Assert.False(registration.IsEnabled());

        registration.SetEnabled(true);
        Assert.True(File.Exists(EntryPath));
        Assert.True(registration.IsEnabled());

        registration.SetEnabled(false);
        Assert.False(File.Exists(EntryPath));
        Assert.False(registration.IsEnabled());
    }

    /// <summary>Disabling something that was never enabled is the ordinary case on first run, and must
    /// not throw its way into the settings toggle.</summary>
    [Fact]
    public void Linux_SetEnabled_False_OnAMissingEntryDoesNothing() {
        Registration().SetEnabled(false);

        Assert.False(Directory.Exists(_directory) && File.Exists(EntryPath));
    }

    /// <summary>The autostart directory does not exist on a fresh account, so writing has to create it
    /// rather than fail.</summary>
    [Fact]
    public void Linux_SetEnabled_CreatesTheAutostartDirectory() {
        Registration().SetEnabled(true);

        Assert.True(Directory.Exists(_directory));
    }

    /// <summary>A denied or unusable location degrades to "not enabled" exactly as the Windows arm's
    /// locked-down registry does — the contract the Settings toggle relies on.</summary>
    [Fact]
    public void Linux_SoftFailsOnAnUnusableDirectory() {
        var registration = new LinuxStartupRegistration(
            Path.Combine(Path.GetTempPath(), "dd-file-not-dir-" + Guid.NewGuid().ToString("N"), "\0bad"));

        registration.SetEnabled(true);

        Assert.False(registration.IsEnabled());
    }

    /// <summary>The spec's own way of switching an entry off without deleting it. Reading the file as
    /// enabled anyway would leave the toggle claiming something login does not do.</summary>
    [Fact]
    public void Linux_IsEnabled_HonoursAHiddenEntry() {
        Registration().SetEnabled(true);
        File.AppendAllText(EntryPath, DesktopEntry.HiddenKey + "\n");

        Assert.False(Registration().IsEnabled());
    }

    // ----- The .desktop body -----

    /// <summary>The keys a desktop environment needs to treat this as a launchable autostart entry.</summary>
    [Fact]
    public void DesktopEntry_CarriesTheKeysAutostartNeeds() {
        var body = DesktopEntry.Build("/opt/dashdetective/DashDetective");

        Assert.StartsWith("[Desktop Entry]", body, StringComparison.Ordinal);
        Assert.Contains("Type=Application", body, StringComparison.Ordinal);
        Assert.Contains("Name=DashDetective", body, StringComparison.Ordinal);
        Assert.Contains("Exec=\"/opt/dashdetective/DashDetective\"", body, StringComparison.Ordinal);
    }

    /// <summary>An unquoted path with a space parses as two arguments, so the entry would launch the
    /// wrong thing — and a home directory with a space in it is ordinary.</summary>
    [Fact]
    public void DesktopEntry_QuotesAnExecPathWithASpace() =>
        Assert.Contains("Exec=\"/home/My User/DashDetective\"",
                        DesktopEntry.Build("/home/My User/DashDetective"),
                        StringComparison.Ordinal);

    /// <summary>The four characters the spec reserves inside a quoted value. Left raw, a "$" in a path
    /// would be expanded by the launcher into something that does not exist.</summary>
    [Theory]
    [InlineData("/home/a$b/App", "\"/home/a\\$b/App\"")]
    [InlineData("/home/a`b/App", "\"/home/a\\`b/App\"")]
    [InlineData("/home/a\"b/App", "\"/home/a\\\"b/App\"")]
    [InlineData("/home/a\\b/App", "\"/home/a\\\\b/App\"")]
    public void DesktopEntry_EscapesTheReservedCharacters(string execPath, string expected) =>
        Assert.Contains($"Exec={expected}", DesktopEntry.Build(execPath), StringComparison.Ordinal);

    [Fact]
    public void DesktopEntry_IsEnabled_ReadsTheHiddenKey() {
        Assert.True(DesktopEntry.IsEnabled(DesktopEntry.Build("/opt/App")));
        Assert.False(DesktopEntry.IsEnabled(DesktopEntry.Build("/opt/App") + DesktopEntry.HiddenKey));
    }
}
