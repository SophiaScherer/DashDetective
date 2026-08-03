namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// Reads a physical drive's temperature. Implementations must never throw: anything unavailable —
/// a non-NVMe drive, an unsupported controller, access denied, an implausible reading — is
/// <c>null</c>, which the UI renders as "—".
/// </summary>
internal interface IDiskTemperatureProvider {
    /// <summary>Composite temperature in °C for physical drive <paramref name="deviceId"/>, or
    /// <c>null</c> when unavailable. Synchronous by design: it is called per-disk on a slow sub-tick
    /// of a timer the caller already owns, so the caller picks its own thread and cadence.</summary>
    double? ReadCelsius(int deviceId);
}
