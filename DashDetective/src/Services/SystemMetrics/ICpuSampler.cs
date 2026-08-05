namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// A source of total (all-cores) CPU utilisation as a percentage (0–100). Lets
/// <see cref="CpuUsageSampler"/> swap between the Task-Manager-matching PDH counter, its
/// <c>GetSystemTimes</c> fallback and <see cref="LinuxCpuSampler"/> behind one seam, and lets tests
/// inject a fake.
/// </summary>
internal interface ICpuSampler {
    /// <summary>Returns CPU utilisation (0–100) for the interval since the previous call.</summary>
    double Sample();
}

/// <summary>The no-data arm: a platform with no CPU reader yet reports 0, which the Dashboard renders as
/// a flat line rather than failing. Windows and Linux both have real implementations.</summary>
internal sealed class UnsupportedCpuSampler : ICpuSampler {
    public double Sample() => 0;
}
