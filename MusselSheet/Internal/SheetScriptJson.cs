namespace MusselSheet.Internal;

using System.Text.Json;

// JS interop serializes arguments and results by reflection, and a trimmed WebAssembly app loses what that needs
// (the constructor parameter names of records and anonymous types). So what crosses to JS is already JSON.
internal static class SheetScriptJson
{
    public static JsonElement ToElement(SheetMap value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.SheetMap);

    public static JsonElement ToElement(ScriptOptionsData value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.ScriptOptionsData);

    public static JsonElement ToElement(CommitResult value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.CommitResult);

    public static JsonElement ToElement(CustomEditorResult value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.CustomEditorResult);

    public static JsonElement ToElement(PasteResult value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.PasteResult);

    public static JsonElement ToElement(ClipboardText value) => JsonSerializer.SerializeToElement(value, SheetScriptJsonContext.Default.ClipboardText);
}
