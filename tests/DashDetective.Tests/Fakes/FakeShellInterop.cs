using DashDetective.Tabs.FileExplorer;
using System;
using System.Collections.Generic;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IShellInterop"/> for headless tests: records every type-name lookup, so how
/// often the shell is actually asked can be pinned. Answers from the extension, as the real shells do.
/// </summary>
internal sealed class FakeShellInterop : IShellInterop {
    /// <summary>Every path handed to <see cref="GetTypeName"/>, in order.</summary>
    public List<string> TypeNameCalls { get; } = [];

    public string GetTypeName(string path, bool isDirectory) {
        TypeNameCalls.Add(path);
        if (isDirectory)
            return "File folder";

        var dot = path.LastIndexOf('.');
        return dot < 0 ? "File" : $"{path[(dot + 1)..].ToUpperInvariant()} File";
    }

    public void Open(string path) { }

    public void ShowProperties(IntPtr owner, string path) { }
}
