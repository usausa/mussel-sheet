namespace Example.Reports;

using Example.Client.Models;

using OysterReport;

// Runs where the fonts are: on the server in both render modes, or in the browser in the standalone demo
public static class InvoiceReportBuilder
{
    private static readonly string TemplatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Reports", "Invoice.xlsx");

    private static readonly ReportRenderOption Options = CreateOptions(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts"));

    public static byte[] Build(InvoiceData invoice)
    {
        using var workbook = new TemplateWorkbook(TemplatePath, Options);
        return Render(workbook, invoice, Options);
    }

    public static byte[] Build(InvoiceData invoice, Stream template, string fontDirectory)
    {
        var options = CreateOptions(fontDirectory);
        using var workbook = new TemplateWorkbook(template, options);
        return Render(workbook, invoice, options);
    }

    // The template uses Windows fonts; the PDF uses Noto Sans JP, the same as the web sheet
    private static ReportRenderOption CreateOptions(string fontDirectory) => new()
    {
        UseSystemFonts = false,
        FontDirectories = [fontDirectory],
        HeaderFooterFallbackFonts = ["Noto Sans JP"],
        FontAliases = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Meiryo"] = ["Noto Sans JP"],
            ["Calibri"] = ["Noto Sans JP"]
        }
    };

    private static byte[] Render(TemplateWorkbook workbook, InvoiceData invoice, ReportRenderOption options)
    {
        var sheet = workbook.GetSheet(0);
        sheet.ReplacePlaceholders(invoice.Fields);
        sheet.ReplacePlaceholders(invoice.Lines);

        using var output = new MemoryStream();
        new OysterReportEngine { RenderingOptions = options }.GeneratePdf(workbook, output);
        return output.ToArray();
    }
}
