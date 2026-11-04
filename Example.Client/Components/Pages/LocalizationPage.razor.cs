namespace Example.Client.Components.Pages;

public sealed partial class LocalizationPage
{
    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(7, 2);
        sheet.Columns[0].Header = "Item";
        sheet.Columns[1].Header = "Input";
        sheet.Columns[0].Width = 120;
        sheet.Columns[1].Width = 180;

        sheet.Range("A:A").Configure(static c =>
        {
            c.Kind = CellKind.Label;
            c.Style.Bold = true;
            c.Style.FillColor = "#eef2f7";
        });
        sheet.Range("B1").Configure(static c => c.Required = true);
        sheet.Range("B2").Configure(static c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N1";
            c.Minimum = 0;
            c.Maximum = 10000;
        });
        sheet.Range("B3").Configure(static c => c.Kind = CellKind.Date);
        sheet.Range("B4").Configure(static c => c.Kind = CellKind.Time);
        sheet.Range("B5").Configure(static c =>
        {
            c.Kind = CellKind.List;
            c.Items = ["Red", "Green", "Blue"];
        });
        sheet.Range("B6").Configure(static c =>
        {
            c.Kind = CellKind.Mask;
            c.Mask = "999-9999";
        });
        sheet.Range("B7").Configure(static c => c.ReadOnly = true);

        sheet.SetValues(0, 0,
        [
            ["Required", "Text"],
            ["Number", 1234.5m],
            ["Date", new DateOnly(2026, 11, 5)],
            ["Time", new TimeOnly(9, 30)],
            ["List", "Green"],
            ["Mask", null],
            ["Read-only", "Read-only"],
        ]);
        return sheet;
    }
}
