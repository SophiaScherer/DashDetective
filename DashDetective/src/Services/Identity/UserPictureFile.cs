using DashDetective.Services.Diagnostics;
using System;
using System.IO;

namespace DashDetective.Services.Identity;

/// <summary>Shared read for both platform arms: the same size cap and the same soft-fail, so a picture
/// found through the registry and one found at <c>~/.face</c> are subject to identical rules.</summary>
internal static class UserPictureFile {
    /// <summary>Anything larger than this is not a portrait tile — a wallpaper left at the path, or a
    /// file that is not an image at all. Refused rather than decoded, so a bad path cannot balloon memory
    /// for a 32px avatar.</summary>
    private const long MaxBytes = 8 * 1024 * 1024;

    /// <summary>The file's bytes, or <c>null</c> if it is missing, oversized or unreadable.</summary>
    public static byte[]? TryRead(string? path) {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length == 0 || file.Length > MaxBytes)
                return null;

            return File.ReadAllBytes(path);
        } catch (Exception e) {
            Log.Warn($"Could not read the account picture at {path}", e);
            return null;
        }
    }
}
