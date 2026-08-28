using System;

namespace DashDetective.Tabs.Processes;

/// <summary>
/// Encodes which column the table is sorted by and in which direction, as one opaque string — so
/// <c>AppSettings</c> stays free of any knowledge of what a process sort key is, exactly as it does for
/// the column order and the collapsed sections.
/// </summary>
public static class ProcessSortState {
    private const char Separator = (char)0x1F;
    private const string Ascending = "Asc";
    private const string Descending = "Desc";

    public static string Encode(ProcessSortKey key, bool ascending) =>
        key.ToString() + Separator + (ascending ? Ascending : Descending);

    /// <summary>The saved sort read back, or false when the record names no column this table has —
    /// a hand-edit, or a column dropped in a later release. The caller then keeps its own default.</summary>
    public static bool TryDecode(string? encoded, out ProcessSortKey key, out bool ascending) {
        key = ProcessSortKey.Name;
        ascending = true;
        if (string.IsNullOrEmpty(encoded))
            return false;

        var fields = encoded.Split(Separator);
        if (fields.Length != 2 || !Enum.TryParse(fields[0], out key) || !Enum.IsDefined(key))
            return false;

        ascending = fields[1] != Descending;
        return true;
    }
}
