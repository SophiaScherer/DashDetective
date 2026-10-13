using System;
using System.Globalization;

namespace DashDetective.Services.SystemMetrics;

/// <summary>
/// One Windows <c>\GPU Engine(*)</c> counter instance, parsed from a name like
/// <c>pid_1234_luid_0x00000000_0x0000E7BE_phys_0_eng_3_engtype_Copy</c>. PDH publishes one instance per
/// (process, physical engine), so <see cref="Luid"/> + <see cref="Phys"/> + <see cref="Engine"/> name a
/// single engine and <see cref="Type"/> only labels it: one adapter can carry several engines of a type.
///
/// Pure and outside the Windows-gated samplers, so it is tested on every CI leg.
/// </summary>
/// <param name="Pid">The owning process, or null when the name carries none.</param>
/// <param name="Luid">The adapter token in <see cref="GpuAdapter.FormatLuidToken"/>'s lower-case form, so it
/// joins the inventory whatever casing PDH used.</param>
/// <param name="Phys">The physical adapter index under that LUID.</param>
/// <param name="Engine">The engine index on that adapter.</param>
/// <param name="Type">The engine type as PDH spells it ("3D", "Copy", "Video Codec 0").</param>
internal readonly record struct GpuEngineInstance(int? Pid, string Luid, int Phys, int Engine, string Type) {
    private const string PidToken = "pid_";
    private const string LuidToken = "luid_0x";
    private const string LuidSeparator = "_0x";
    private const string PhysToken = "_phys_";
    private const string EngineToken = "_eng_";
    private const string TypeToken = "_engtype_";

    /// <summary>Parses an instance name; false for anything malformed or missing a LUID, phys index, engine
    /// index or type. Never throws.</summary>
    public static bool TryParse(string? instanceName, out GpuEngineInstance instance) {
        instance = default;
        if (string.IsNullOrEmpty(instanceName))
            return false;

        var head = instanceName.AsSpan();

        // Taken from the end, so a type is everything after the LAST "_engtype_".
        var typeAt = head.LastIndexOf(TypeToken, StringComparison.OrdinalIgnoreCase);
        if (typeAt < 0)
            return false;
        var type = head[(typeAt + TypeToken.Length)..];
        if (type.IsWhiteSpace())
            return false;
        head = head[..typeAt];

        if (!TrySplitTrailingIndex(ref head, EngineToken, out var engine) ||
            !TrySplitTrailingIndex(ref head, PhysToken, out var phys))
            return false;

        // What is left is "[pid_<n>_]luid_0x<hex>_0x<hex>".
        var luidAt = head.IndexOf(LuidToken, StringComparison.OrdinalIgnoreCase);
        if (luidAt < 0 || !TryParseLuid(head[(luidAt + LuidToken.Length)..], out var luid))
            return false;

        int? pid = null;
        var prefix = head[..luidAt];
        if (!prefix.IsEmpty) {
            if (!prefix.StartsWith(PidToken, StringComparison.OrdinalIgnoreCase) || prefix[^1] != '_' ||
                !TryParseIndex(prefix[PidToken.Length..^1], out var parsedPid))
                return false;
            pid = parsedPid;
        }

        instance = new GpuEngineInstance(pid, luid, phys, engine, type.ToString());
        return true;
    }

    /// <summary>Splits "<c>…{token}{digits}</c>" off the end of <paramref name="head"/>.</summary>
    private static bool TrySplitTrailingIndex(ref ReadOnlySpan<char> head, string token, out int value) {
        value = 0;
        var at = head.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (at < 0 || !TryParseIndex(head[(at + token.Length)..], out value))
            return false;
        head = head[..at];
        return true;
    }

    /// <summary>Digits only: no sign, no whitespace, and nothing wider than an int.</summary>
    private static bool TryParseIndex(ReadOnlySpan<char> digits, out int value) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    /// <summary>Parses "<c>{high}_0x{low}</c>" and re-formats it, so the token always has eight lower-case
    /// digits per half. Shared with <see cref="GpuMemoryUsage"/>, so both counters key on one token.</summary>
    internal static bool TryParseLuid(ReadOnlySpan<char> halves, out string luid) {
        luid = "";
        var separator = halves.IndexOf(LuidSeparator, StringComparison.OrdinalIgnoreCase);
        if (separator < 0 ||
            !uint.TryParse(halves[..separator], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                out var high) ||
            !uint.TryParse(halves[(separator + LuidSeparator.Length)..], NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out var low))
            return false;

        luid = GpuAdapter.FormatLuidToken(unchecked((int)high), low);
        return true;
    }
}
