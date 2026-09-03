using DashDetective.Services.Threading;
using System;

namespace DashDetective.Services.Notifications;

/// <summary>
/// The one place a completed action says so. A page hands it a message; the shell renders it as the
/// confirmation banner under the toolbar and this clears it again after <see cref="Duration"/>.
///
/// Cross-cutting in the <c>ThemeService</c> shape — Settings, Toolkit and the shell all raise notices,
/// and only the shell draws one — so it lives here rather than in a tab. Expiry runs on
/// <see cref="IUiTimer"/> rather than <c>DispatcherTimer.RunOnce</c>, which no headless test can drive.
/// </summary>
internal sealed class NoticeService {
    /// <summary>How long a confirmation stays up. Long enough to read a path, short enough that it is
    /// gone before it becomes furniture.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

    private readonly IUiTimer _expiry;

    public NoticeService() : this(new DispatcherTimerAdapter()) { }

    internal NoticeService(IUiTimer expiry) {
        _expiry = expiry;
        _expiry.Interval = Duration;
        _expiry.Tick += (_, _) => Dismiss();
    }

    /// <summary>The message on show, or null when nothing is.</summary>
    public string? Current { get; private set; }

    /// <summary>Raised whenever <see cref="Current"/> changes, with its new value.</summary>
    public event Action<string?>? Changed;

    /// <summary>Shows a confirmation, replacing whatever was up and restarting its window. Replaced
    /// rather than queued: the second action is the one the user just took, and a queue would report
    /// them out of step with the clicks that caused them.</summary>
    public void Show(string message) {
        if (message.Length == 0)
            return;

        _expiry.Stop();
        Current = message;
        _expiry.Start();
        Changed?.Invoke(Current);
    }

    /// <summary>Takes the current confirmation down — the banner's ×, Esc, or the window elapsing.</summary>
    public void Dismiss() {
        _expiry.Stop();
        if (Current is null)
            return;

        Current = null;
        Changed?.Invoke(null);
    }
}
