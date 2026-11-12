namespace Example.Reports;

using Example.Client.Models;

using OysterReport;

// Runs on the server, where the fonts are, in both render modes
public static class InvoiceReportBuilder
{
    private static readonly string TemplatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Reports", "Invoice.xlsx");

    // The template uses Windows fonts; the PDF uses Noto Sans JP, the same as the web sheet
    private static readonly ReportRenderOption Options = new()
    {
        UseSystemFonts = false,
        FontDirectories = [Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts")],
        HeaderFooterFallbackFonts = ["Noto Sans JP"],
        FontAliases = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Meiryo"] = ["Noto Sans JP"],
            ["Calibri"] = ["Noto Sans JP"]
        }
    };

    public static byte[] Build(InvoiceData invoice)
    {
        using var workbook = new TemplateWorkbook(TemplatePath, Options);
        var sheet = workbook.GetSheet(0);
        sheet.ReplacePlaceholders(invoice.Fields);
        sheet.ReplacePlaceholders(invoice.Lines);

        using var output = new MemoryStream();
        new OysterReportEngine { RenderingOptions = Options }.GeneratePdf(workbook, output);
        return output.ToArray();
    }
}
