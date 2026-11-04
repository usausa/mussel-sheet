namespace Example.Client.Models;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(InvoiceData))]
public sealed partial class InvoiceJsonContext : JsonSerializerContext;
