namespace Example.Endpoints;

using Example.Client.Models;
using Example.Reports;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Reports);

        group.MapPost("/invoice", HandleInvoice);
    }

    private static IResult HandleInvoice(InvoiceData invoice) =>
        Results.File(InvoiceReportBuilder.Build(invoice), "application/pdf", "invoice.pdf");
}
