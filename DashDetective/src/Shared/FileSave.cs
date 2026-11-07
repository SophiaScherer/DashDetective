using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DashDetective.Services.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DashDetective.Shared;

/// <summary>
/// The one save-file flow: offer the formats, let the native dialog pick a destination, then write what
/// the chosen extension asked for. Replaces three near-identical copies (the toolbar Export, the two
/// Settings export buttons, the Toolkit log export), which is what made adding a format an edit in three
/// places.
///
/// Lives in the view layer because a save dialog needs the window's <c>TopLevel</c>. Soft-failing: a cancelled
/// pick, a failed render or a failed write all leave the app untouched.
/// </summary>
internal static class FileSave {
    /// <summary>
    /// Saves text chosen by format. <paramref name="content"/> is called only after a destination is
    /// picked, and only for the one format that was chosen, so the other renderings are never built.
    /// </summary>
    /// <param name="owner">Any control in the window that owns the dialog.</param>
    /// <param name="title">The dialog's title.</param>
    /// <param name="suggestedName">The filename to offer, without an extension.</param>
    /// <param name="formats">The formats to offer, in order; the first is the default.</param>
    /// <param name="content">Renders the report in whichever format the chosen filename means.</param>
    /// <returns>Where the file was written, for a caller that confirms it; null for a cancelled pick or
    /// a failed write, which are the two things there is nothing to announce about.</returns>
    public static Task<string?> SaveAsync(Visual owner, string title, string suggestedName,
                                          IReadOnlyList<DiagnosticsFormat> formats,
                                          Func<DiagnosticsFormat, string> content) =>
        GuardAsync(() => SaveCoreAsync(owner, title, suggestedName, formats, content));

    /// <summary>Never throws. Deliberately broad: the picker and the open can fail in ways a platform
    /// defines, and every caller is an <c>async void</c> handler where a throw would end the process.</summary>
    internal static async Task<string?> GuardAsync(Func<Task<string?>> save) {
        try {
            return await save();
        } catch (Exception e) {
            Log.Error("Export failed", e);
            return null;
        }
    }

    private static async Task<string?> SaveCoreAsync(Visual owner, string title, string suggestedName,
                                                     IReadOnlyList<DiagnosticsFormat> formats,
                                                     Func<DiagnosticsFormat, string> content) {
        var storage = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage is null || formats.Count == 0)
            return null;

        var choices = new FilePickerFileType[formats.Count];
        for (var i = 0; i < formats.Count; i++) {
            var info = DiagnosticsFormats.Info(formats[i]);
            choices[i] = new FilePickerFileType(info.TypeName) { Patterns = [$"*.{info.Extension}"] };
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = DiagnosticsFormats.Info(formats[0]).Extension,
            FileTypeChoices = choices,
        });

        if (file is null)
            return null; // user cancelled

        // The name is the authority on the format: the dialog does not report which filter was
        // chosen, and a typed extension should win over the one that happened to be selected.
        var format = DiagnosticsFormats.FromFileName(file.Name);

        if (!await TryWriteAsync(file.OpenWriteAsync, () => content(format)))
            return null;

        // The local path where there is one, so a confirmation can name somewhere the user can go look.
        // A picker on a provider that has no local file (a cloud location) still has a name.
        return file.TryGetLocalPath() ?? file.Name;
    }

    /// <summary>Renders first, then opens the destination, so a throwing renderer cannot leave a truncated file; any failure returns false.</summary>
    internal static async Task<bool> TryWriteAsync(Func<Task<Stream>> openWrite, Func<string> render) {
        string text;
        try {
            text = render();
        } catch (Exception e) {
            // Broad on purpose: a renderer can throw anything and callers cannot survive an escape. Nothing is truncated, as the destination is not yet open.
            Log.Error("Export could not be rendered", e);
            return false;
        }

        try {
            await using var stream = await openWrite();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(text);
            return true;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // Disk full, permission denied, drive removed mid-write: the app carries on.
            Log.Warn("Export could not be written", e);
            return false;
        }
    }
}
