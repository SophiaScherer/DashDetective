using DashDetective.Tabs.Network;
using DashDetective.Tests.Fakes;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DashDetective.Tests.Tabs.Network;

/// <summary>Pins the public-address lookup's contract: each family is read from its own https endpoint,
/// only a well-formed address of the family asked for is accepted, and every failure (offline, a bad
/// body, a refusing server, the timeout) comes back as <c>null</c> for that family rather than throwing.</summary>
public class PublicAddressProviderTests {
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    private static (PublicAddressProvider Provider, FakeHttpHandler Handler) Create(
        Func<Uri, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null) {
        var handler = new FakeHttpHandler { Respond = respond };
        return (new PublicAddressProvider(new HttpClient(handler), timeout ?? DefaultTimeout), handler);
    }

    private static Task<HttpResponseMessage> Answer(Uri uri, string v4, string v6) =>
        Task.FromResult(FakeHttpHandler.Text(uri.Host == PublicAddressProvider.Ipv4Endpoint.Host ? v4 : v6));

    [Fact]
    public async Task GetAsync_ReturnsBothFamilies() {
        var (provider, _) = Create((uri, _) => Answer(uri, "203.0.113.7\n", "2001:db8::7"));

        var result = await provider.GetAsync();

        Assert.Equal("203.0.113.7", result.Ipv4);
        Assert.Equal("2001:db8::7", result.Ipv6);
    }

    [Fact]
    public async Task GetAsync_AsksEachFamilyItsOwnHttpsEndpoint() {
        var (provider, handler) = Create((uri, _) => Answer(uri, "203.0.113.7", "2001:db8::7"));

        await provider.GetAsync();

        Assert.Equal(2, handler.Requested.Count);
        Assert.Contains(handler.Requested, u => u.Scheme == "https" && u.Host == "api.ipify.org");
        Assert.Contains(handler.Requested, u => u.Scheme == "https" && u.Host == "api6.ipify.org");
    }

    [Fact]
    public async Task GetAsync_V4OnlyNetwork_LeavesIpv6Null() {
        var (provider, _) = Create((uri, _) => uri.Host == PublicAddressProvider.Ipv6Endpoint.Host
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("no route to host"))
            : Answer(uri, "203.0.113.7", ""));

        var result = await provider.GetAsync();

        Assert.Equal("203.0.113.7", result.Ipv4);
        Assert.Null(result.Ipv6);
    }

    [Theory]
    [InlineData("<html>Please sign in to the Wi-Fi</html>")]
    [InlineData("")]
    [InlineData("999.1.1.1")]
    [InlineData("not an address")]
    public async Task GetAsync_GarbageBody_IsUnavailable(string body) {
        var (provider, _) = Create((uri, _) => Answer(uri, body, body));

        var result = await provider.GetAsync();

        Assert.Equal(PublicAddresses.None, result);
    }

    /// <summary><c>IPAddress.TryParse</c> accepts these as IPv4, so the shape check is what rejects them.</summary>
    [Theory]
    [InlineData("12345")]
    [InlineData("1.2")]
    [InlineData("1.2.3")]
    [InlineData("0x7f.0.0.1")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.2.3.0004")]
    public async Task GetAsync_NonCanonicalIpv4_IsUnavailable(string body) {
        var (provider, _) = Create((uri, _) => Answer(uri, body, "2001:db8::7"));

        var result = await provider.GetAsync();

        Assert.Null(result.Ipv4);
        Assert.Equal("2001:db8::7", result.Ipv6);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1.2")]
    [InlineData("203.0.113.7")]
    public async Task GetAsync_Ipv6EndpointAnswerWithoutAColon_IsUnavailable(string body) {
        var (provider, _) = Create((uri, _) => Answer(uri, "203.0.113.7", body));

        var result = await provider.GetAsync();

        Assert.Equal("203.0.113.7", result.Ipv4);
        Assert.Null(result.Ipv6);
    }

    /// <summary>A body past the cap is refused even when what precedes the padding is a valid address.</summary>
    [Fact]
    public async Task GetAsync_OversizedBody_IsUnavailable() {
        var padded = "203.0.113.7" + new string(' ', 5000);
        var (provider, _) = Create((uri, _) => Answer(uri, padded, padded));

        var result = await provider.GetAsync();

        Assert.Equal(PublicAddresses.None, result);
    }

    /// <summary>The cap is on what is read, not only on the verdict: a body that never ends must not hang
    /// or be buffered whole.</summary>
    [Fact]
    public async Task GetAsync_EndlessBody_IsUnavailableWithoutReadingItAll() {
        var (provider, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamContent(new EndlessStream()),
        }));

        var result = await provider.GetAsync();

        Assert.Equal(PublicAddresses.None, result);
    }

    /// <summary>Each lookup opens its own connection, so a network switch shows on the next ask.</summary>
    [Fact]
    public async Task GetAsync_AsksTheServerToCloseTheConnection() {
        var (provider, handler) = Create((uri, _) => Answer(uri, "203.0.113.7", "2001:db8::7"));

        await provider.GetAsync();

        Assert.Equal(2, handler.ConnectionClose.Count);
        Assert.All(handler.ConnectionClose, close => Assert.True(close));
    }

    /// <summary>The first send can stall on proxy detection, so the request must not start on the caller's thread.</summary>
    [Fact]
    public void GetAsync_StartsTheRequestOffTheCallingThread() {
        var caller = Environment.CurrentManagedThreadId;
        var seen = new System.Collections.Concurrent.ConcurrentBag<int>();
        var (provider, _) = Create((uri, _) => {
            seen.Add(Environment.CurrentManagedThreadId);
            return Answer(uri, "203.0.113.7", "2001:db8::7");
        });

        // Block this thread until the work is done, so it cannot be the one that ran it.
        var task = provider.GetAsync();
        ((IAsyncResult)task).AsyncWaitHandle.WaitOne();

        Assert.Equal(2, seen.Count);
        Assert.DoesNotContain(caller, seen);
    }

    private sealed class EndlessStream : System.IO.Stream {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) {
            Array.Fill(buffer, (byte)'1', offset, count);
            return count;
        }
        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>An IPv4 answer on the IPv6 endpoint (and the reverse) is not what was asked for.</summary>
    [Fact]
    public async Task GetAsync_WrongFamily_IsUnavailable() {
        var (provider, _) = Create((uri, _) => Answer(uri, "2001:db8::7", "203.0.113.7"));

        var result = await provider.GetAsync();

        Assert.Null(result.Ipv4);
        Assert.Null(result.Ipv6);
    }

    [Fact]
    public async Task GetAsync_ServerError_IsUnavailable() {
        var (provider, _) = Create((_, _) =>
            Task.FromResult(FakeHttpHandler.Text("203.0.113.7", HttpStatusCode.ServiceUnavailable)));

        var result = await provider.GetAsync();

        Assert.Equal(PublicAddresses.None, result);
    }

    [Fact]
    public async Task GetAsync_WhenTheRequestThrows_ReturnsNullsRatherThanThrowing() {
        var (provider, _) = Create((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));

        var result = await provider.GetAsync();

        Assert.Equal(PublicAddresses.None, result);
    }

    [Fact]
    public async Task GetAsync_WhenTheServerNeverAnswers_GivesUpAtTheTimeout() {
        var (provider, _) = Create(async (_, token) => {
            await Task.Delay(Timeout.Infinite, token);
            return FakeHttpHandler.Text("203.0.113.7");
        }, TimeSpan.FromMilliseconds(300));

        var result = await provider.GetAsync();

        Assert.Equal(PublicAddresses.None, result);
    }

    [Fact]
    public async Task GetAsync_WhenTheCallerCancels_ReturnsNullsRatherThanThrowing() {
        using var cts = new CancellationTokenSource();
        var (provider, _) = Create(async (_, token) => {
            await Task.Delay(Timeout.Infinite, token);
            return FakeHttpHandler.Text("203.0.113.7");
        });

        var pending = provider.GetAsync(cts.Token);
        cts.Cancel();

        Assert.Equal(PublicAddresses.None, await pending);
    }

    /// <summary>The families are read concurrently: a v6 attempt that hangs must not delay the v4 answer
    /// past the timeout, and v4 is still reported.</summary>
    [Fact]
    public async Task GetAsync_OneFamilyHanging_StillReportsTheOther() {
        var (provider, _) = Create(async (uri, token) => {
            if (uri.Host == PublicAddressProvider.Ipv6Endpoint.Host)
                await Task.Delay(Timeout.Infinite, token);
            return FakeHttpHandler.Text("203.0.113.7");
        }, TimeSpan.FromMilliseconds(300));

        var result = await provider.GetAsync();

        Assert.Equal("203.0.113.7", result.Ipv4);
        Assert.Null(result.Ipv6);
    }
}
