namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A source of total (all-cores) CPU utilisation as a percentage (0–100). Lets
/// <see cref="CpuUsageSampler"/> swap between the Task-Manager-matching PDH counter and the
/// <c>GetSystemTimes</c> fallback behind one seam, and lets tests inject a fake.
/// </summary>
internal interface ICpuSampler {
    /// <summary>Returns CPU utilisation (0–100) for the interval since the previous call.</summary>
    double Sample();
}
