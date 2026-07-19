using System.Reflection;

namespace DashDetective.Shared;

/// <summary>
/// Read-only facts about the running application, sourced from the entry assembly's metadata (set in
/// the csproj) so the UI never hard-codes a product name or version. Centralising this here keeps the
/// Settings footer honest — it reports the assembly it was actually built from rather than a literal
/// string that drifts from reality.
/// </summary>
public static class AppInfo {
    /// <summary>The product name shown in the UI.</summary>
    public const string Name = "DashDetective";

    /// <summary>
    /// The informational version of the entry assembly (from <c>&lt;Version&gt;</c> in the csproj),
    /// e.g. "0.1.0". Any build-metadata suffix ("+&lt;hash&gt;") is trimmed, and a sensible fallback
    /// is used if the attribute can't be read (e.g. in the visual designer).
    /// </summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion() {
        var assembly = Assembly.GetEntryAssembly();

        // Prefer the informational version (mirrors <Version>); fall back to the assembly version.
        var informational = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational)) {
            // Strip SourceLink-style build metadata ("0.1.0+abc1234") down to the bare version.
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var version = assembly?.GetName().Version;
        return version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.1.0";
    }
}
