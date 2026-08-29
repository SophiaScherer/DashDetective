namespace DashDetective.Shared;

/// <summary>How wall-clock times read on screen. Persisted as a user preference and applied live.</summary>
public enum ClockFormat {
    /// <summary>24-hour, e.g. "16:05:09". The default.</summary>
    TwentyFourHour,

    /// <summary>12-hour with an AM/PM suffix, e.g. "4:05:09 PM".</summary>
    TwelveHour,
}
