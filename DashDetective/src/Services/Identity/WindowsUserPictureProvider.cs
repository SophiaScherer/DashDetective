using DashDetective.Services.Diagnostics;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace DashDetective.Services.Identity;

/// <summary>
/// Reads the Windows account picture for the current user. Windows indexes the tiles it generated under
/// the machine's <c>AccountPicture\Users\{SID}</c> key, one registry value per size, each holding a path;
/// that index is the authoritative source, so it is tried first. Uses the in-box
/// <c>Microsoft.Win32.Registry</c> API, and reads only — nothing here writes. The platform check lives in
/// <see cref="IUserPictureProvider.ForCurrentPlatform"/>, which is why there is no guard in here.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsUserPictureProvider : IUserPictureProvider {
    private const string AccountPictureKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users";

    /// <summary>The tile sizes to try, largest usable first. Starts at 448 rather than the 1080 Windows
    /// also stores: the footer avatar is 32px, so decoding a megapixel portrait for it would be waste.
    /// Descends so a machine that only kept the small tiles still yields something.</summary>
    private static readonly string[] SizeValues =
        ["Image448", "Image240", "Image192", "Image96", "Image64", "Image48", "Image32"];

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    public byte[]? Read() {
        try {
            var sid = CurrentSid();
            if (sid is null)
                return null;

            return UserPictureFile.TryRead(FromRegistry(sid)) ?? UserPictureFile.TryRead(FromPublicFolder(sid));
        } catch (Exception e) {
            Log.Warn("Could not read the Windows account picture", e);
            return null;
        }
    }

    /// <summary>The interactive user's SID, which is how both stores key the picture.</summary>
    private static string? CurrentSid() {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value;
    }

    /// <summary>The first tile path the index offers, or <c>null</c> when the user has no picture set.</summary>
    private static string? FromRegistry(string sid) {
        using var key = Registry.LocalMachine.OpenSubKey($@"{AccountPictureKey}\{sid}");
        if (key is null)
            return null;

        foreach (var value in SizeValues)
            if (key.GetValue(value) is string path && !string.IsNullOrWhiteSpace(path))
                return path;

        return null;
    }

    /// <summary>Where the tiles themselves live, for the case where the index is missing or stale but the
    /// files are not. Keyed by SID, so this can only ever find the current user's own picture — and the
    /// folder exists only once a picture has actually been set, so it cannot return the default silhouette.
    /// The largest file wins, since the names encode a size this does not need to parse.</summary>
    private static string? FromPublicFolder(string sid) {
        var root = Environment.GetEnvironmentVariable("PUBLIC");
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var directory = new DirectoryInfo(Path.Combine(root, "AccountPictures", sid));
        if (!directory.Exists)
            return null;

        FileInfo? largest = null;
        foreach (var file in directory.EnumerateFiles())
            if (Array.IndexOf(ImageExtensions, file.Extension.ToLowerInvariant()) >= 0 &&
                (largest is null || file.Length > largest.Length))
                largest = file;

        return largest?.FullName;
    }
}

/// <summary>The no-account-picture set: reports that there is none, so the footer keeps its initials
/// badge. What every platform without an implemented arm gets.</summary>
internal sealed class UnsupportedUserPictureProvider : IUserPictureProvider {
    public byte[]? Read() => null;
}
