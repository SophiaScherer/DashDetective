using DashDetective.Services.Identity;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace DashDetective.Tests.Services.Identity;

/// <summary>
/// Covers the <see cref="IUserPictureProvider"/> seam: which reader the platform resolves to, the size
/// cap and soft-fail both arms share, and the Linux arm's lookup order.
///
/// The Linux arm is portable <c>System.IO</c> over two injected roots, so its whole lookup runs on a
/// Windows dev machine — only the *default* roots are Linux-shaped, and those are not what these
/// exercise. The Windows arm reads the machine's own registry and account, which a test cannot stage, so
/// it is covered only by the resolution test; its behaviour on a machine with no picture is the same
/// <c>null</c> every other arm returns.
/// </summary>
public class UserPictureProviderTests : IDisposable {
    private readonly string _home =
        Path.Combine(Path.GetTempPath(), "dd-home-" + Guid.NewGuid().ToString("N"));

    private readonly string _icons =
        Path.Combine(Path.GetTempPath(), "dd-icons-" + Guid.NewGuid().ToString("N"));

    public UserPictureProviderTests() {
        Directory.CreateDirectory(_home);
        Directory.CreateDirectory(_icons);
    }

    public void Dispose() {
        foreach (var directory in new[] { _home, _icons })
            try {
                Directory.Delete(directory, recursive: true);
            } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
                // A leftover temp folder is not worth failing a test over.
            }
        GC.SuppressFinalize(this);
    }

    private LinuxUserPictureProvider Provider() => new(_home, _icons);

    private void WriteHomeFile(string name, string content) =>
        File.WriteAllText(Path.Combine(_home, name), content);

    [Fact]
    public void ForCurrentPlatform_ResolvesTheReaderForThisHost() {
        var provider = IUserPictureProvider.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsUserPictureProvider>(provider);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxUserPictureProvider>(provider);
        else
            Assert.IsType<UnsupportedUserPictureProvider>(provider);
    }

    [Fact]
    public void Unsupported_ReportsNoPicture() =>
        Assert.Null(new UnsupportedUserPictureProvider().Read());

    /// <summary>The ordinary case on an account that never set a picture: no file anywhere, and the
    /// footer keeps its initials badge rather than the read throwing into the shell's constructor.</summary>
    [Fact]
    public void Linux_NoPictureAnywhere_ReturnsNull() =>
        Assert.Null(Provider().Read());

    [Fact]
    public void Linux_ReadsTheHomeFaceFile() {
        WriteHomeFile(".face", "portrait");

        Assert.Equal("portrait", Encoding.UTF8.GetString(Provider().Read()!));
    }

    [Fact]
    public void Linux_FallsBackToFaceIcon() {
        WriteHomeFile(".face.icon", "icon");

        Assert.Equal("icon", Encoding.UTF8.GetString(Provider().Read()!));
    }

    /// <summary>AccountsService caches a copy the display manager wrote, which may be older than what the
    /// user last set in their home directory — so the home file has to win when both exist.</summary>
    [Fact]
    public void Linux_PrefersTheHomeFileOverAccountsService() {
        WriteHomeFile(".face", "home");
        File.WriteAllText(Path.Combine(_icons, Environment.UserName), "accountsservice");

        Assert.Equal("home", Encoding.UTF8.GetString(Provider().Read()!));
    }

    [Fact]
    public void Linux_FallsBackToTheAccountsServiceIcon() {
        File.WriteAllText(Path.Combine(_icons, Environment.UserName), "accountsservice");

        Assert.Equal("accountsservice", Encoding.UTF8.GetString(Provider().Read()!));
    }

    /// <summary>An empty file is not a picture, and decoding one would only fail further downstream.</summary>
    [Fact]
    public void Linux_EmptyFile_ReturnsNull() {
        WriteHomeFile(".face", "");

        Assert.Null(Provider().Read());
    }

    /// <summary>Nothing this large is a portrait tile. Refused before the read so a wallpaper left at the
    /// path cannot balloon memory for a 32px avatar.</summary>
    [Fact]
    public void Linux_OversizedFile_ReturnsNull() {
        File.WriteAllBytes(Path.Combine(_home, ".face"), new byte[(8 * 1024 * 1024) + 1]);

        Assert.Null(Provider().Read());
    }

    /// <summary>A directory where a file was expected is exactly the shape of a half-configured host, and
    /// it must degrade to "no picture" rather than throw.</summary>
    [Fact]
    public void Linux_DirectoryInPlaceOfTheFile_ReturnsNull() {
        Directory.CreateDirectory(Path.Combine(_home, ".face"));

        Assert.Null(Provider().Read());
    }

    /// <summary>A missing home directory is what a reader sees on a locked-down or freshly imaged host.</summary>
    [Fact]
    public void Linux_MissingRoots_ReturnNull() =>
        Assert.Null(new LinuxUserPictureProvider(
            Path.Combine(Path.GetTempPath(), "dd-absent-" + Guid.NewGuid().ToString("N")),
            Path.Combine(Path.GetTempPath(), "dd-absent-" + Guid.NewGuid().ToString("N"))).Read());
}
