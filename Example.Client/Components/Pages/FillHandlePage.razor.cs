namespace Example.Client.Components.Pages;

public sealed partial class FillHandlePage
{
    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private bool fillHandleEnabled = true;

    private static Sheet CreateSheet()
    {
        const int rows = 12;
        string[] headers = ["No", "Date", "Round", "Part No.", "Quantity", "Owner"];
        int[] widths = [60, 120, 90, 100, 90, 100];

        var sheet = new Sheet(rows, headers.Length);
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Range("1:1").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.Bold = true;
                c.Style.FillColor = "#eef2f7";
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A2:A12").Configure(static c => c.Kind = CellKind.Number);
            sheet.Range("B2:B12").Configure(static c => c.Kind = CellKind.Date);
            sheet.Range("E2:E12").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
            });
            sheet.SetValues(0, 0, [headers]);
            sheet.SetValues(1, 0,
            [
                [1m, new DateOnly(2026, 11, 3), "Round 1", "A-001", 10m, "Smith"],
                [2m, null, null, null, 20m]
            ]);

            // Column A has data down to row 12, so a double click on the handle of column B fills that far
            for (var r = 3; r < rows; r++)
            {
                sheet.SetValue(r, 0, (decimal)r);
            }
        }

        return sheet;
    }
}
