namespace MusselSheet;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using MusselSheet.Internal;

public static class SheetDesignJson
{
    private static readonly JsonTypeInfo<SheetDesign> TypeInfo = CreateTypeInfo();

    public static string Write(SheetDesign design) => JsonSerializer.Serialize(design, TypeInfo);

    public static SheetDesign Read(string json)
    {
        var design = JsonSerializer.Deserialize(json, TypeInfo) ?? throw new JsonException("The JSON has no sheet design.");
        return HasNullItem(design) ? throw new JsonException("The JSON has a null in a list.") : design;
    }

    private static bool HasNullItem(SheetDesign design) =>
        HasNull(design.Rows) || HasNull(design.Columns) || HasNull(design.Rules) || HasNull(design.Merges) || HasNull(design.Values) || HasNull(design.Computations) ||
        (design.Rules?.Any(static x => HasNull(x.Items)) is true) || (design.Computations?.Any(static x => HasNull(x.Observed)) is true);

    private static bool HasNull<T>(IReadOnlyList<T?>? items)
        where T : class =>
        (items is not null) && items.Any(static x => x is null);

    private static JsonTypeInfo<SheetDesign> CreateTypeInfo()
    {
        var options = new JsonSerializerOptions(SheetDesignJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NewLine = "\n",
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
#if NET11_0_OR_GREATER
        return options.GetTypeInfo<SheetDesign>();
#else
        return (JsonTypeInfo<SheetDesign>)options.GetTypeInfo(typeof(SheetDesign));
#endif
    }
}
