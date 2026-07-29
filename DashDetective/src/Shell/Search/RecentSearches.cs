using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DashDetective.Shell.Search;

/// <summary>One thing the user opened from search before, enough to list it and find it again.</summary>
/// <param name="Category">Which kind of thing it was.</param>
/// <param name="Key">Its identity within that category — see <see cref="SearchResult.Identity"/>.</param>
/// <param name="Title">The headline as it was shown.</param>
/// <param name="Subtitle">The line of context as it was shown.</param>
public sealed record RecentSearch(SearchCategory Category, string Key, string Title, string Subtitle);

/// <summary>
/// The last few things opened from the search box, shown when it is focused with nothing typed.
///
/// Entries are stored rather than re-derived, but they are never *acted on* directly: opening one runs
/// the search again and matches the result by identity, so a file that has been deleted or a process
/// that has exited simply isn't found and the entry drops itself. That keeps a stale list from
/// promising something the machine can no longer do.
///
/// Persisted as flat strings so <c>AppSettings</c> needs no knowledge of search — the encoding lives
/// here, next to the type it encodes.
/// </summary>
public sealed class RecentSearches {
    /// <summary>How many to keep. Enough to cover a session's worth of jumping about, few enough that
    /// the dropdown stays a glance rather than a list to read.</summary>
    public const int MaxEntries = 8;

    // ASCII unit separator: a control character, so it cannot occur in a file path, a process name or
    // any label — which is what makes joining the fields without escaping them safe.
    private const char FieldSeparator = (char)0x1F;

    /// <summary>ASCII record separator, dividing one entry from the next. Same reasoning as the field
    /// separator above.</summary>
    private const char EntrySeparator = (char)0x1E;

    private const int FieldCount = 4;

    /// <summary>Most recently opened first.</summary>
    public ObservableCollection<RecentSearch> Entries { get; } = new();

    /// <summary>Raised whenever the list changes, so the composition root can persist it.</summary>
    public event Action? Changed;

    /// <summary>Puts a result at the top of the list, moving it there if it was already present, and
    /// drops the oldest once the list is full.</summary>
    public void Remember(SearchResult result) {
        var entry = new RecentSearch(result.Category, result.Identity, result.Title, result.Subtitle);
        Remember(entry);
    }

    /// <summary>Puts an entry at the top of the list. Used when re-opening a recent, which promotes it
    /// the same way opening a fresh result does.</summary>
    public void Remember(RecentSearch entry) {
        RemoveMatching(entry.Category, entry.Key);
        Entries.Insert(0, entry);

        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(Entries.Count - 1);

        Changed?.Invoke();
    }

    /// <summary>Drops an entry that no longer names anything — the file is gone, the process exited.</summary>
    public void Forget(RecentSearch entry) {
        if (RemoveMatching(entry.Category, entry.Key))
            Changed?.Invoke();
    }

    /// <summary>Replaces the list with what was persisted, newest first. Silently skips anything that
    /// doesn't decode, so a hand-edited or older settings file costs its bad entries and nothing more.</summary>
    public void Load(string? encoded) {
        Entries.Clear();
        if (string.IsNullOrEmpty(encoded))
            return;

        foreach (var line in encoded.Split(EntrySeparator)) {
            if (Entries.Count >= MaxEntries)
                break;
            if (TryDecode(line, out var entry))
                Entries.Add(entry);
        }
    }

    /// <summary>The whole list as one persistable string, newest first.</summary>
    public string Encode() {
        var lines = new List<string>(Entries.Count);
        foreach (var entry in Entries)
            lines.Add(string.Join(
                FieldSeparator, entry.Category.ToString(), entry.Key, entry.Title, entry.Subtitle));

        return string.Join(EntrySeparator, lines);
    }

    internal static bool TryDecode(string? line, out RecentSearch entry) {
        entry = null!;
        if (string.IsNullOrEmpty(line))
            return false;

        var fields = line.Split(FieldSeparator);
        if (fields.Length != FieldCount ||
            !Enum.TryParse<SearchCategory>(fields[0], out var category) ||
            fields[1].Length == 0)
            return false;

        entry = new RecentSearch(category, fields[1], fields[2], fields[3]);
        return true;
    }

    private bool RemoveMatching(SearchCategory category, string key) {
        for (var i = 0; i < Entries.Count; i++)
            if (Entries[i].Category == category &&
                string.Equals(Entries[i].Key, key, StringComparison.OrdinalIgnoreCase)) {
                Entries.RemoveAt(i);
                return true;
            }

        return false;
    }
}
