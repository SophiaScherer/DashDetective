using DashDetective.Services.Platform.Linux;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IProcFileSystem"/> for headless tests: a dictionary of absolute path → file
/// body, so a Linux provider can be driven against canned <c>/proc</c> and <c>/sys</c> fixtures from a
/// Windows dev box. Anything not staged degrades exactly as the real one does — <c>null</c> / empty,
/// never a throw <b>unless a test stages one</b> with <see cref="ThrowOn"/>.
///
/// That opt-in matters: the real <see cref="ProcFileSystem"/> swallows I/O failures itself, so the only
/// way a provider's own <c>catch</c> is ever reached is a fake that can break its contract on demand.
/// Without it, thirteen providers' soft-fail paths were unreachable from any test.
///
/// Directory listings are <b>derived</b> from the staged paths rather than staged separately, so a test
/// cannot describe a tree the real filesystem could not produce: staging
/// <c>/sys/devices/system/cpu/cpu0/online</c> is what makes <c>ListDirectory("/sys/devices/system/cpu")</c>
/// report <c>cpu0</c>.
/// </summary>
internal sealed class FakeProcFileSystem : IProcFileSystem {
    private readonly Dictionary<string, string> _files = [];
    private readonly Dictionary<string, string> _links = [];
    private readonly List<(string Prefix, Exception Error)> _throws = [];

    /// <summary>Every path read through <see cref="ReadAllText"/> or <see cref="ReadAllLines"/>, in order —
    /// how a test pins that a provider read the source it claims to.</summary>
    public List<string> Reads { get; } = [];

    /// <summary>Stages a file. Returns <c>this</c> so a fixture tree reads as one expression.</summary>
    public FakeProcFileSystem WithFile(string path, string content) {
        _files[path] = content;
        return this;
    }

    /// <summary>Stages a symlink for <see cref="ResolveLink"/>.</summary>
    public FakeProcFileSystem WithLink(string path, string target) {
        _links[path] = target;
        return this;
    }

    /// <summary>Removes a staged symlink — how a test models a descriptor closing between two reads, which
    /// under <c>/proc</c> is the ordinary case rather than the exceptional one.</summary>
    public FakeProcFileSystem WithoutLink(string path) {
        _links.Remove(path);
        return this;
    }

    /// <summary>
    /// Makes every read of a path starting with <paramref name="pathPrefix"/> throw, so a provider's own
    /// soft-fail can be reached. Defaults to the failure that actually happens under <c>/proc</c>: a file
    /// whose permissions deny the read.
    /// </summary>
    public FakeProcFileSystem ThrowOn(string pathPrefix, Exception? error = null) {
        _throws.Add((pathPrefix, error ?? new UnauthorizedAccessException($"denied: {pathPrefix}")));
        return this;
    }

    public bool Exists(string path) =>
        _files.ContainsKey(path) || _links.ContainsKey(path) || ChildrenOf(path).Any();

    public string? ReadAllText(string path) {
        Reads.Add(path);
        ThrowIfStaged(path);
        return _files.TryGetValue(path, out var content) ? content : null;
    }

    public IReadOnlyList<string> ReadAllLines(string path) {
        Reads.Add(path);
        ThrowIfStaged(path);
        if (!_files.TryGetValue(path, out var content))
            return [];

        // Matches File.ReadAllLines: \r\n and \n both split, and a trailing newline yields no empty entry.
        var lines = content.Replace("\r\n", "\n").Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    public IReadOnlyList<string> ListDirectory(string path) {
        ThrowIfStaged(path);
        return [.. ChildrenOf(path)];
    }

    public string? ResolveLink(string path) {
        ThrowIfStaged(path);
        return _links.TryGetValue(path, out var target) ? target : null;
    }

    private void ThrowIfStaged(string path) {
        foreach (var (prefix, error) in _throws)
            if (path.StartsWith(prefix, StringComparison.Ordinal))
                throw error;
    }

    /// <summary>The distinct first path segments beneath <paramref name="path"/>, across both staged
    /// files and staged links — the derivation that stands in for a directory listing. Sorted, unlike a
    /// real listing, purely so assertions are stable; no provider may rely on the order.</summary>
    private IEnumerable<string> ChildrenOf(string path) {
        var prefix = path.EndsWith('/') ? path : path + '/';
        return _files.Keys
            .Concat(_links.Keys)
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(key => key[prefix.Length..].Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }
}
