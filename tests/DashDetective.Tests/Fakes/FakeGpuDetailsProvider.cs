using DashDetective.Services.SystemMetrics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>Hand-rolled <see cref="IGpuDetailsProvider"/>: reports whatever details a test stages, records
/// what it was asked, and can break its never-throw contract on demand.</summary>
internal sealed class FakeGpuDetailsProvider : IGpuDetailsProvider {
    private readonly Dictionary<string, GpuDetails> _details = new(StringComparer.Ordinal);
    private Exception? _failure;

    /// <summary>Every request, in order — how a test tells "asked and got nothing" from "never asked".</summary>
    public List<IReadOnlyCollection<string>> Requests { get; } = [];

    public FakeGpuDetailsProvider With(string adapterKey, GpuDetails details) {
        _details[adapterKey] = details;
        return this;
    }

    public FakeGpuDetailsProvider Throwing(string why = "the details read failed") {
        _failure = new InvalidOperationException(why);
        return this;
    }

    public Task<IReadOnlyDictionary<string, GpuDetails>> GetAsync(IReadOnlyCollection<string> adapterKeys) {
        Requests.Add(adapterKeys);
        if (_failure is { } failure)
            return Task.FromException<IReadOnlyDictionary<string, GpuDetails>>(failure);

        var result = new Dictionary<string, GpuDetails>(StringComparer.Ordinal);
        foreach (var key in adapterKeys)
            if (_details.TryGetValue(key, out var details))
                result[key] = details;
        return Task.FromResult<IReadOnlyDictionary<string, GpuDetails>>(result);
    }
}
