namespace MusselSheet;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using MusselSheet.Internal;

public static class SheetDesignJson
{
    private static readonly JsonTypeInfo<SheetDesign> TypeInfo = CreateTypeInfo();

    public static string Write(SheetDesign design) => JsonSerializer.Serialize(design, TypeInfo);

    public static SheetDesign Read(string json) =>
        JsonSerializer.Deserialize(json, TypeInfo) ?? throw new JsonException("The JSON has no sheet design.");

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
