using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A page's set of <see cref="SystemMetricsService"/> subscriptions, as factories rather than tokens so
/// they can be dropped and re-established as the page comes and goes. Dropping them is what actually
/// stops the shared feed: the service ref-counts subscribers and a channel with none stops sampling, so
/// a deactivated page that merely ignored its callbacks would still be paying for them.
///
/// <see cref="Attach"/> and <see cref="Detach"/> are idempotent. Re-attaching replays each feed's cached
/// latest sample, so a page returning to screen seeds with real data rather than a blank frame.
/// </summary>
internal sealed class MetricSubscriptions(params Func<IDisposable>[] subscribe) : IDisposable {
    private IDisposable[]? _tokens;

    /// <summary>Whether the subscriptions are currently established.</summary>
    public bool IsAttached => _tokens is not null;

    public void Attach() {
        if (_tokens is not null)
            return;

        var tokens = new IDisposable[subscribe.Length];
        for (var i = 0; i < subscribe.Length; i++)
            tokens[i] = subscribe[i]();
        _tokens = tokens;
    }

    public void Detach() {
        if (_tokens is null)
            return;

        foreach (var token in _tokens)
            token.Dispose();
        _tokens = null;
    }

    public void Dispose() => Detach();
}
