namespace MusselSheet.Internal;

using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SheetDesign))]
internal sealed partial class SheetDesignJsonContext : JsonSerializerContext;
