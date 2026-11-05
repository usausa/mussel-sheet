namespace MusselSheet;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using MusselSheet.Internal;

// UnsafeRelaxedJsonEscaping on purpose: this is a file format, so nothing is escaped for HTML embedding; an app
// that puts the JSON into HTML has to escape it itself. NewLine is fixed to LF so the output does not depend
// on the OS.
public static class SheetDesignJson
{
    private static readonly JsonTypeInfo<SheetDesign> TypeInfo = CreateTypeInfo();

    public static string Write(SheetDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);
        return JsonSerializer.Serialize(design, TypeInfo);
    }

    public static SheetDesign Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, TypeInfo) ?? throw new JsonException("The JSON has no sheet design.");
    }

    private static JsonTypeInfo<SheetDesign> CreateTypeInfo()
    {
        var options = new JsonSerializerOptions(SheetDesignJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NewLine = "\n"
        };
        return (JsonTypeInfo<SheetDesign>)options.GetTypeInfo(typeof(SheetDesign));
    }
}
