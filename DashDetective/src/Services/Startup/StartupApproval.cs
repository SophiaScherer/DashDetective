namespace DashDetective.Services.Startup;

/// <summary>
/// Decodes the Task Manager "Startup apps" switch, which Windows keeps apart from the <c>Run</c> value
/// in <c>HKCU\...\Explorer\StartupApproved\Run</c>. Pure, so every CI leg can test it.
/// </summary>
internal static class StartupApproval {
    /// <summary>Whether the binary value marks the entry disabled. The first byte carries the state:
    /// 0x02 or 0x06 enabled, 0x03 or 0x07 disabled, so the low bit is the switch. A missing or empty
    /// value is enabled, which is how Windows reads an entry it never wrote a state for.</summary>
    internal static bool IsDisabled(byte[]? value) => value is { Length: > 0 } && (value[0] & 1) == 1;
}
