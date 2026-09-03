using DashDetective.Services.Links;
using System;
using System.Collections.Generic;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IWebLinkOpener"/> for headless tests: records every URL and reports whatever
/// the test staged. <see cref="Succeeds"/> is the failure mode, so a caller's failure path is reachable.
/// </summary>
internal sealed class FakeWebLinkOpener : IWebLinkOpener {
    /// <summary>Every URL asked for, in order.</summary>
    public List<string> Opened { get; } = [];

    /// <summary>What <see cref="Open"/> reports. Set false to stage a refused or failed launch.</summary>
    public bool Succeeds { get; set; } = true;

    /// <summary>The only URL, when a test expects exactly one.</summary>
    public string Single => Opened.Count == 1
        ? Opened[0]
        : throw new InvalidOperationException($"Expected exactly one link, saw {Opened.Count}.");

    public bool Open(string url) {
        Opened.Add(url);
        return Succeeds;
    }
}
