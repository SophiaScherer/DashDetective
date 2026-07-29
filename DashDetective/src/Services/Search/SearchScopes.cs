using System;
using System.Collections.Generic;
using System.IO;

namespace DashDetective.Services.Search;

/// <summary>
/// Decides which folders a file search covers.
///
/// The user's profile is the whole of it in the normal case — that is where a person's files live, and
/// searching the entire disk would bury real matches under system files. The one addition is the folder
/// the File Explorer is currently showing: someone browsing a data drive and reaching for search means
/// the drive they are looking at, not their home directory.
/// </summary>
public static class SearchScopes {
    /// <summary>The folders to search, given where the File Explorer currently is.</summary>
    public static IReadOnlyList<string> For(string? currentFolder) =>
        For(currentFolder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Test seam: takes the profile folder explicitly rather than reading the environment.</summary>
    internal static IReadOnlyList<string> For(string? currentFolder, string profileFolder) {
        var scopes = new List<string>(2);

        if (!string.IsNullOrEmpty(profileFolder))
            scopes.Add(profileFolder);

        // Adding a folder already inside the profile would only make the index match it twice.
        if (!string.IsNullOrWhiteSpace(currentFolder) && !IsUnder(currentFolder, profileFolder))
            scopes.Add(currentFolder);

        return scopes;
    }

    // Whether path sits at or below root. Compared on the normalised full paths so "C:\Users\X\..\X"
    // and a trailing separator don't read as somewhere else.
    private static bool IsUnder(string path, string root) {
        if (string.IsNullOrEmpty(root))
            return false;

        try {
            var normalPath = Normalize(path);
            var normalRoot = Normalize(root);

            return normalPath.Equals(normalRoot, StringComparison.OrdinalIgnoreCase) ||
                   normalPath.StartsWith(normalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        } catch {
            // Malformed path: treat it as outside, so it is searched rather than silently dropped.
            return false;
        }
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
