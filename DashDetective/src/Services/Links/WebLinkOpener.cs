using DashDetective.Services.Diagnostics;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace DashDetective.Services.Links;

/// <summary>
/// The real <see cref="IWebLinkOpener"/>. https only, the same rule as <c>ToolkitRunner</c>: the shell
/// acts on any scheme it has an association for, so a caller's typo must not become an arbitrary launch.
/// </summary>
internal sealed class WebLinkOpener : IWebLinkOpener {
    private const string HttpsPrefix = "https://";

    private readonly Action<string> _start;

    public WebLinkOpener() : this(Launch) { }

    /// <param name="start">How a URL reaches the OS. Faked in tests; production uses the shell.</param>
    internal WebLinkOpener(Action<string> start) => _start = start;

    public bool Open(string url) {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        try {
            _start(url);
            return true;
        } catch (Exception error) when (error is Win32Exception or InvalidOperationException or ObjectDisposedException) {
            // No association, a denied launch, or a process gone as it started. The caller reports the
            // failure; the reason is only kept here.
            Log.Warn($"Could not open link: {url}", error);
            return false;
        }
    }

    private static void Launch(string url) {
        using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
