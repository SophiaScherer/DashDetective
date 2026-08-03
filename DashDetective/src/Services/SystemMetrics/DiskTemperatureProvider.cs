namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// TEMPORARY façade over <see cref="IDiskTemperatureProvider"/>, kept for exactly one commit so the
/// provider body could move behind the seam without touching any call site. The next phase routes
/// <c>StorageViewModel</c> through the injected bundle and deletes this file.
/// </summary>
public static class DiskTemperatureProvider {
    public static double? ReadCelsius(int deviceId) =>
        HardwareProviders.ForCurrentPlatform().DiskTemperature.ReadCelsius(deviceId);
}
