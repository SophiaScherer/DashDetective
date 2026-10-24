using DashDetective.Tabs.Network;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="IPublicAddressProvider"/> for headless tests. Answers instantly with
/// <see cref="Result"/> by default; <see cref="Throwing"/> breaks the contract on demand, and
/// <see cref="Hold"/> makes the next call wait on a gate the test releases, to stage out-of-order replies.
/// </summary>
internal sealed class FakePublicAddressProvider : IPublicAddressProvider {
    private readonly Queue<TaskCompletionSource<PublicAddresses>> _held = new();

    /// <summary>How many times the page asked.</summary>
    public int Calls { get; private set; }

    /// <summary>What an unheld call answers.</summary>
    public PublicAddresses Result { get; set; } = new("203.0.113.7", "2001:db8::7");

    /// <summary>Set to make every unheld call throw, which a real provider never does.</summary>
    public bool Throwing { get; set; }

    /// <summary>The next call waits for the returned source instead of answering at once.</summary>
    public TaskCompletionSource<PublicAddresses> Hold() {
        var gate = new TaskCompletionSource<PublicAddresses>();
        _held.Enqueue(gate);
        return gate;
    }

    public Task<PublicAddresses> GetAsync(CancellationToken token = default) {
        Calls++;
        if (_held.Count > 0)
            return _held.Dequeue().Task;
        return Throwing
            ? Task.FromException<PublicAddresses>(new System.InvalidOperationException("staged failure"))
            : Task.FromResult(Result);
    }
}
