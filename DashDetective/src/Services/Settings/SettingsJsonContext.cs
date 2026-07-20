using System.Text.Json.Serialization;

namespace DashDetective.Services.Settings;

/// <summary>
/// System.Text.Json source-generation context for <see cref="AppSettings"/>. Using the generated
/// metadata (rather than reflection-based serialization) keeps trimming/AOT analysis clean under the
/// project's warning-as-error gate, and <c>UseStringEnumConverter</c> writes enums as readable names
/// (e.g. <c>"Dark"</c>, <c>"Left"</c>) so the file is legible and stable across enum reordering.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext {
}
