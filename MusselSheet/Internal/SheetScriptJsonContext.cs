namespace MusselSheet.Internal;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Serialization, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SheetMap))]
[JsonSerializable(typeof(ScriptLanguage))]
[JsonSerializable(typeof(ScriptOptionsData))]
[JsonSerializable(typeof(CommitResult))]
[JsonSerializable(typeof(CustomEditorResult))]
[JsonSerializable(typeof(PasteResult))]
[JsonSerializable(typeof(ClipboardText))]
internal sealed partial class SheetScriptJsonContext : JsonSerializerContext;
