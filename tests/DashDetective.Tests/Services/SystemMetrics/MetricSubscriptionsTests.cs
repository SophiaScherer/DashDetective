using DashDetective.Services.SystemMetrics;
using System;
using Xunit;

namespace DashDetective.Tests.Services.SystemMetrics;

/// <summary>Pins <see cref="MetricSubscriptions"/>: subscriptions are established on demand rather than at
/// construction, attach/detach are idempotent, and detaching really disposes the tokens — that disposal is
/// what lets the ref-counted feeds stop when a page leaves the screen.</summary>
public class MetricSubscriptionsTests {
    /// <summary>Stands in for a feed's subscription token, recording that it was released.</summary>
    private sealed class Token : IDisposable {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Construction_SubscribesNothing() {
        var calls = 0;
        var subscriptions = new MetricSubscriptions(() => {
            calls++;
            return new Token();
        });

        Assert.False(subscriptions.IsAttached);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Attach_CallsEveryFactoryOnce_AndIsIdempotent() {
        var calls = 0;
        var subscriptions = new MetricSubscriptions(
            () => { calls++; return new Token(); },
            () => { calls++; return new Token(); });

        subscriptions.Attach();
        subscriptions.Attach();

        Assert.True(subscriptions.IsAttached);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Detach_DisposesEveryToken_AndIsIdempotent() {
        var tokens = new[] { new Token(), new Token() };
        var next = 0;
        var subscriptions = new MetricSubscriptions(() => tokens[next++], () => tokens[next++]);
        subscriptions.Attach();

        subscriptions.Detach();
        subscriptions.Detach();

        Assert.False(subscriptions.IsAttached);
        Assert.All(tokens, t => Assert.True(t.Disposed));
    }

    [Fact]
    public void Reattach_TakesFreshTokens() {
        var created = 0;
        var subscriptions = new MetricSubscriptions(() => {
            created++;
            return new Token();
        });

        subscriptions.Attach();
        subscriptions.Detach();
        subscriptions.Attach();

        Assert.True(subscriptions.IsAttached);
        Assert.Equal(2, created);
    }

    [Fact]
    public void Dispose_ReleasesAnAttachedSet() {
        var token = new Token();
        var subscriptions = new MetricSubscriptions(() => token);
        subscriptions.Attach();

        subscriptions.Dispose();

        Assert.True(token.Disposed);
        Assert.False(subscriptions.IsAttached);
    }
}
