using DashDetective.Services.Diagnostics;
using System;

namespace DashDetective.Services.SystemMetrics;

/// <summary>Selects the inert overload of a sampler's constructor. Single-valued on purpose, so the test
/// seam cannot be used to claim a sampler is ready without real native counters.</summary>
internal enum SamplerInit {
    /// <summary>Skip native initialisation; the sampler returns its zero/empty contract forever.</summary>
    Inert,
}

/// <summary>
/// Shared soft-fail helper for the native samplers. Each sampler already checks native <c>return codes</c>;
/// this covers the separate failure to load or bind the library at all, so a broken PDH installation — or a
/// host without <c>pdh.dll</c> — leaves a sampler inert instead of throwing out of a field initialiser.
/// Deliberately narrow: a genuine Win32 bug still surfaces rather than being swallowed.
/// </summary>
internal static class NativeLoadFailure {
    /// <summary>True for the three exceptions that mean "this native library isn't usable here". Used as a
    /// <c>catch</c> filter so nothing broader is ever caught.</summary>
    internal static bool Matches(Exception error) =>
        error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

    /// <summary>Records a sampler going inert. Called from constructors (or a one-shot latch), so it never
    /// becomes per-tick log spam.</summary>
    internal static void Report(string sampler, Exception error) =>
        Log.Warn($"{sampler}: native counters unavailable, sampler inert", error);
}
