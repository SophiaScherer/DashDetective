using System.Text.Json.Serialization;

namespace DashDetective.Services.Diagnostics;

/// <summary>
/// System.Text.Json source-generation context for <see cref="DiagnosticsReport"/>. Generated metadata
/// rather than reflection-based serialization, for the same reason as <c>SettingsJsonContext</c>: it is
/// what keeps trimming/AOT analysis clean under the project's warnings-as-errors gate.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DiagnosticsReport))]
internal sealed partial class DiagnosticsJsonContext : JsonSerializerContext {
}
