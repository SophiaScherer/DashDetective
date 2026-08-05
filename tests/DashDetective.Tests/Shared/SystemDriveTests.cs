using DashDetective.Shared;
using System;
using System.IO;
using Xunit;

namespace DashDetective.Tests.Shared;

/// <summary>Covers <see cref="SystemDrive"/>: the system root is a usable rooted path on every
/// platform, and the drive letter it pairs with stays Windows-shaped.</summary>
public class SystemDriveTests {
    [Fact]
    public void Root_IsARootedPath() {
        // The point of Root is that a caller can hand it straight to DriveInfo or a capacity label,
        // which Environment.SystemDirectory cannot do off Windows — it is empty there.
        Assert.NotEmpty(SystemDrive.Root);
        Assert.True(Path.IsPathRooted(SystemDrive.Root));
    }

    [Fact]
    public void Root_NamesADirectoryThatExists() {
        Assert.True(Directory.Exists(SystemDrive.Root));
    }

    [Fact]
    public void Root_IsTheSoleRootOffWindows() {
        if (OperatingSystem.IsWindows())
            return;

        Assert.Equal("/", SystemDrive.Root);
    }

    [Fact]
    public void Root_AgreesWithLetterOnWindows() {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal(SystemDrive.Letter, char.ToUpperInvariant(SystemDrive.Root[0]));
    }

    [Fact]
    public void Letter_IsALetter() {
        Assert.True(char.IsLetter(SystemDrive.Letter));
    }
}
