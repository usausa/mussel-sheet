namespace Example.Client.Models;

// What the Japanese Form page sends to the server for the PDF. The keys are the placeholders of the template
public sealed record InvoiceData(
    IReadOnlyDictionary<string, string?> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Lines);
