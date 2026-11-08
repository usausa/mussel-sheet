namespace Example.Client.Models;

// The keys are the placeholders of the invoice template
public sealed record InvoiceData(
    IReadOnlyDictionary<string, string?> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Lines);
