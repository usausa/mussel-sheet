namespace MusselSheet.Internal;

using System.Text.Json;

// Already JSON, as interop's reflection loses record parameter names in a trimmed WebAssembly app
internal static class SheetScriptJson
{
    public static JsonElement ToElement(SheetMap value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.SheetMap);

    public static JsonElement ToElement(ScriptLanguage value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.ScriptLanguage);

    public static JsonElement ToElement(ScriptOptionsData value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.ScriptOptionsData);

    public static JsonElement ToElement(CommitResult value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.CommitResult);

    public static JsonElement ToElement(CustomEditorResult value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.CustomEditorResult);

    public static JsonElement ToElement(PasteResult value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.PasteResult);

    public static JsonElement ToElement(ClipboardText value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.ClipboardText);
}
