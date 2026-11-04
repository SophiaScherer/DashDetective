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
/// Lives in the view layer because a save dialog needs the window's <c>TopLevel</c>. Fully soft-failing,
/// like the copies it replaces: a cancelled pick and a failed I/O write both leave the app untouched.
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
    public static async Task<string?> SaveAsync(Visual owner, string title, string suggestedName,
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

    /// <summary>
    /// Renders first, then opens the destination, so a renderer that throws cannot leave a truncated file
    /// behind. A renderer failure is a bug and propagates; only a failed open or write returns false.
    /// </summary>
    internal static async Task<bool> TryWriteAsync(Func<Task<Stream>> openWrite, Func<string> render) {
        var text = render();
        try {
            await using var stream = await openWrite();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(text);
            return true;
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            // Disk full, permission denied, drive removed mid-write: the file isn't written and the app
            // carries on. Anything else is a bug and is left to surface.
            Log.Warn("Export could not be written", e);
            return false;
        }
    }
}
