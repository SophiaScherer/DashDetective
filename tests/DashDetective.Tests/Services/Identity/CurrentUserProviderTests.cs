using DashDetective.Services.Identity;
using DashDetective.Tests.Fakes;
using System;
using Xunit;

namespace DashDetective.Tests.Services.Identity;

/// <summary>Covers <see cref="CurrentUserProvider.DeriveInitials"/> (two-token, single-token, one-char and
/// empty inputs, all upper-cased) and the role: an administrator reads as one whether or not the process is
/// elevated, on Windows from the token's deny-only Administrators SID and on Linux from root or an
/// administrative group, and anything that cannot be read is the neutral "User".</summary>
public class CurrentUserProviderTests {
    [Theory]
    [InlineData("sophia.schmidt", "SS")]
    [InlineData("a b", "AB")]
    [InlineData("john-paul", "JP")]
    [InlineData("sophia_schmidt-jones", "SS")]   // 3+ tokens → first two tokens' initials
    public void DeriveInitials_TwoOrMoreTokens_UsesFirstLetterOfFirstTwo(string name, string expected) {
        Assert.Equal(expected, CurrentUserProvider.DeriveInitials(name));
    }

    [Fact]
    public void DeriveInitials_SingleToken_UsesFirstTwoLetters() {
        Assert.Equal("SO", CurrentUserProvider.DeriveInitials("sophiasch"));
    }

    [Fact]
    public void DeriveInitials_OneCharacter_UsesThatLetter() {
        Assert.Equal("X", CurrentUserProvider.DeriveInitials("x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveInitials_Empty_ReturnsQuestionMark(string name) {
        Assert.Equal("?", CurrentUserProvider.DeriveInitials(name));
    }

    // By name: AdminStatus is internal, and a public test method cannot take it as a parameter.
    [Theory]
    [InlineData(nameof(AdminStatus.Elevated), "Administrator")]
    [InlineData(nameof(AdminStatus.Member), "Administrator")]
    [InlineData(nameof(AdminStatus.Standard), "Standard User")]
    [InlineData(nameof(AdminStatus.Unknown), "User")]
    public void RoleLabel_NamesTheAccountNotTheProcess(string status, string expected) =>
        Assert.Equal(expected, CurrentUserProvider.RoleLabel(Enum.Parse<AdminStatus>(status)));

    [Fact]
    public void WindowsStatus_ElevatedToken_IsElevated() =>
        Assert.Equal(AdminStatus.Elevated, CurrentUserProvider.WindowsStatus(elevated: true, []));

    /// <summary>The bug this pins: UAC runs an administrator's apps with the Administrators group deny-only,
    /// so the role check fails and every administrator read as a standard user.</summary>
    [Fact]
    public void WindowsStatus_FilteredAdministratorToken_IsAMember() =>
        Assert.Equal(AdminStatus.Member,
            CurrentUserProvider.WindowsStatus(elevated: false, ["S-1-5-114", CurrentUserProvider.AdministratorsSid]));

    [Fact]
    public void WindowsStatus_NoAdministratorsSid_IsStandard() =>
        Assert.Equal(AdminStatus.Standard, CurrentUserProvider.WindowsStatus(elevated: false, ["S-1-5-114"]));

    private const string AdminGroups = "root:x:0:\nsudo:x:27:\nwheel:x:10:\nusers:x:100:\n";
    private const string UserStatus = "Uid:\t1000\t1000\t1000\t1000\n";

    private static FakeProcFileSystem Linux(string status, string groups = AdminGroups) =>
        new FakeProcFileSystem().WithFile("/proc/self/status", status).WithFile("/etc/group", groups);

    [Fact]
    public void ReadLinuxStatus_Root_IsElevated() =>
        Assert.Equal(AdminStatus.Elevated,
            CurrentUserProvider.ReadLinuxStatus(Linux("Uid:\t0\t0\t0\t0\nGroups:\t0\n")));

    [Theory]
    [InlineData("27")]
    [InlineData("10")]
    [InlineData("4 24 27 1000")]
    public void ReadLinuxStatus_InAnAdministrativeGroup_IsAMember(string groups) =>
        Assert.Equal(AdminStatus.Member,
            CurrentUserProvider.ReadLinuxStatus(Linux(UserStatus + "Groups:\t" + groups + "\n")));

    [Fact]
    public void ReadLinuxStatus_InNoAdministrativeGroup_IsStandard() =>
        Assert.Equal(AdminStatus.Standard,
            CurrentUserProvider.ReadLinuxStatus(Linux(UserStatus + "Groups:\t100 1000\n")));

    /// <summary>Uid 0 is root, so an unread owner must not be reported as 0 — nor guessed as standard.</summary>
    [Fact]
    public void ReadLinuxStatus_NoStatusFile_IsUnknown() =>
        Assert.Equal(AdminStatus.Unknown,
            CurrentUserProvider.ReadLinuxStatus(new FakeProcFileSystem().WithFile("/etc/group", AdminGroups)));

    // Staged against a status that would otherwise read as standard, so the test fails if the guard goes.
    [Fact]
    public void ReadLinuxStatus_UnreadableGroupFile_IsUnknown() =>
        Assert.Equal(AdminStatus.Unknown,
            CurrentUserProvider.ReadLinuxStatus(
                new FakeProcFileSystem().WithFile("/proc/self/status", UserStatus + "Groups:\t100\n")));

    [Fact]
    public void ReadLinuxStatus_NoGroupsLine_IsUnknown() =>
        Assert.Equal(AdminStatus.Unknown, CurrentUserProvider.ReadLinuxStatus(Linux(UserStatus)));
}
