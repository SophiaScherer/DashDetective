namespace DashDetective.Services.Startup;

/// <summary>Decodes Task Manager's switch in <c>StartupApproved\Run</c>, which Windows keeps apart from the <c>Run</c> value.</summary>
internal static class StartupApproval {
    /// <summary>The first byte's low bit is the disabled flag; a missing value means enabled, as Windows reads it.</summary>
    internal static bool IsDisabled(byte[]? value) => value is { Length: > 0 } && (value[0] & 1) == 1;
}
