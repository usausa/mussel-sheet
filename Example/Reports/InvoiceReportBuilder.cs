namespace Example.Reports;

using Example.Client.Models;

using OysterReport;

// Fills the Excel template with OysterReport. It runs on the server, where the fonts are, in both render modes
public static class InvoiceReportBuilder
{
    private static readonly string TemplatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Reports", "Invoice.xlsx");

    // The template uses Meiryo, a Windows font
    private static readonly ReportRenderOption Options = new()
    {
        FontAliases = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Meiryo"] = ["Noto Sans CJK JP", "Hiragino Sans", "IPAexGothic"],
        },
    };

    public static byte[] Build(InvoiceData invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        using var workbook = new TemplateWorkbook(TemplatePath, Options);
        var sheet = workbook.GetSheet(0);
        sheet.ReplacePlaceholders(invoice.Fields);
        sheet.ReplacePlaceholders(invoice.Lines);

        using var output = new MemoryStream();
        new OysterReportEngine { RenderingOptions = Options }.GeneratePdf(workbook, output);
        return output.ToArray();
    }
}
