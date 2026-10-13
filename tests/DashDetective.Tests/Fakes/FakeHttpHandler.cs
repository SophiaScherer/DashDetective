using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DashDetective.Tests.Fakes;

/// <summary>
/// Hand-rolled <see cref="HttpMessageHandler"/> so a client can be driven without a network. Answers
/// each request from <see cref="Respond"/> and records the URLs; <see cref="Respond"/> may throw or wait
/// on the cancellation token, which is how a test breaks the contract on demand.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler {
    /// <summary>Every URL requested, in order.</summary>
    public List<Uri> Requested { get; } = [];

    /// <summary>How a request is answered. Defaults to an empty 200.</summary>
    public Func<Uri, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(Text(""));

    /// <summary>A 200 with a plain-text body.</summary>
    public static HttpResponseMessage Text(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        lock (Requested)
            Requested.Add(request.RequestUri!);
        return Respond(request.RequestUri!, token);
    }
}
