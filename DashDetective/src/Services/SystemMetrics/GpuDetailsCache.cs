using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Remembers each adapter's <see cref="GpuDetails"/> for the life of the process and asks the platform
/// reader only about adapters it has not seen. Reads are serialized, so a refresh landing during the first
/// read waits for it rather than probing Direct3D a second time. Portable, so the caching is tested on
/// every CI leg; a reader that breaks its never-throw contract caches nothing and the throw reaches the
/// caller.
/// </summary>
internal sealed class GpuDetailsCache(IGpuDetailsProvider reader) : IGpuDetailsProvider {
    private readonly Dictionary<string, GpuDetails> _known = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyDictionary<string, GpuDetails>> GetAsync(IReadOnlyCollection<string> adapterKeys) {
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            var missing = new List<string>();
            foreach (var key in adapterKeys)
                if (!_known.ContainsKey(key))
                    missing.Add(key);

            if (missing.Count > 0) {
                // An adapter the reader returns nothing for is asked about again next time.
                foreach (var (key, details) in await reader.GetAsync(missing).ConfigureAwait(false))
                    _known[key] = details;
            }

            var result = new Dictionary<string, GpuDetails>(StringComparer.Ordinal);
            foreach (var key in adapterKeys)
                if (_known.TryGetValue(key, out var details))
                    result[key] = details;
            return result;
        } finally {
            _gate.Release();
        }
    }
}
