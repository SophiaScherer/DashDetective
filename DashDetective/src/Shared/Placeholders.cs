namespace DashDetective.Shared;

/// <summary>
/// The strings a surface shows when it has no real value to show. Dashboard and Performance each held
/// a private <c>NoReading</c> const of their own, which is the second user the promotion rule waits
/// for; the <c>Unknown*</c> set was written as a bare literal at every site that needed it.
///
/// These are display placeholders, not sentinels: a reader that cannot answer still reports
/// <c>null</c>/<c>""</c>/<c>0</c> honestly and lets its consumer choose the wording, because the right
/// wording genuinely differs (the Processes tab wants "Unknown" for a nameless process where the
/// Network tab wants "PID 1234").
/// </summary>
public static class Placeholders {
    /// <summary>A value that could not be read — the em dash every surface renders for "no reading".
    /// Distinct from a value that is genuinely zero, which is printed as a number.</summary>
    public const string NoReading = "—";

    /// <summary>A named thing whose name could not be determined.</summary>
    public const string Unknown = "Unknown";

    /// <summary>
    /// The processor, as the Dashboard and Performance tabs word it.
    /// <para><b>Note the split:</b> <see cref="UnknownProcessor"/> is the same concept worded
    /// differently by the CPU info providers and the Hardware tab. The two spellings predate this
    /// class and are kept because reconciling them changes text the user sees; they are named here so
    /// the divergence is visible in one place rather than hidden across nine files.</para>
    /// </summary>
    public const string UnknownCpu = "Unknown CPU";

    /// <summary>The processor, as the CPU info providers word it. See <see cref="UnknownCpu"/>.</summary>
    public const string UnknownProcessor = "Unknown processor";

    public const string UnknownGpu = "Unknown GPU";
    public const string UnknownRam = "Unknown RAM";
    public const string UnknownOs = "Unknown OS";
    public const string UnknownBios = "Unknown BIOS";
    public const string UnknownMotherboard = "Unknown motherboard";
}
