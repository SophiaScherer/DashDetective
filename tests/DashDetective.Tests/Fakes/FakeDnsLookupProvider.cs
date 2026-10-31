using DashDetective.Tabs.Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IDnsLookupProvider"/> whose lookups stay pending until the test finishes them,
/// so a slow lookup can land after a newer one. Continuations run inline on completion, which keeps the
/// ordering deterministic. <see cref="Fail"/> is the failure mode: the real provider never throws, but
/// the page still guards the call.
/// </summary>
internal sealed class FakeDnsLookupProvider : IDnsLookupProvider {
    private readonly List<(string Host, TaskCompletionSource<DnsResult> Source)> _pending = [];

    /// <summary>Every host asked for, in order.</summary>
    public List<string> Requests { get; } = [];

    public Task<DnsResult> GetAsync(string host) {
        Requests.Add(host);
        var source = new TaskCompletionSource<DnsResult>();
        _pending.Add((host, source));
        return source.Task;
    }

    /// <summary>Finishes the oldest unfinished lookup of <paramref name="host"/> with an answer naming it.</summary>
    public void Complete(string host) =>
        Take(host).SetResult(new DnsResult($"Name:    {host}", $"Resolved {host}"));

    /// <summary>Finishes the oldest unfinished lookup of <paramref name="host"/> by throwing.</summary>
    public void Fail(string host) => Take(host).SetException(new InvalidOperationException("lookup failed"));

    private TaskCompletionSource<DnsResult> Take(string host) {
        var index = _pending.FindIndex(p => p.Host == host);
        if (index < 0)
            throw new InvalidOperationException($"No pending lookup of {host}.");

        var source = _pending[index].Source;
        _pending.RemoveAt(index);
        return source;
    }
}
