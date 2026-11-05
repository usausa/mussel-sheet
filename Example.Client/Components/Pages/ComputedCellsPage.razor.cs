namespace Example.Client.Components.Pages;

public sealed partial class ComputedCellsPage
{
    private const int FirstDetailRow = 3;

    private const int MaxDetailRows = 10;

    private const decimal TaxRate = 1.1m;

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(8, 4);
        int[] widths = [110, 140, 130, 140];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Range("A1:A2").Configure(Label);
            sheet.Range("C1:C2").Configure(Label);
            sheet.Range("A3:D3").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A7:C8").Configure(Label);
            sheet.Range("B1").Configure(static c => c.Kind = CellKind.Date);
            sheet.Range("D1").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "0.00";
                c.Minimum = 0;
            });
            sheet.Range("D2").Configure(static c => c.Kind = CellKind.Number);
            sheet.Range("B4:C6").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
            });
            sheet.Range("D4:D8").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
            });
            sheet.Range("D8").Configure(static c =>
            {
                c.Style.Bold = true;
                c.StyleProvider = static x => x.Value is > 100000m ? new CellStyle { FontColor = "#c62828" } : null;
            });
            sheet.Range("A1:D2").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A3:D8").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A1:D2").BorderAround(CellBorder.Medium);
            sheet.Range("A3:D8").BorderAround(CellBorder.Medium);
            sheet.SetValues(0, 0,
            [
                ["Date", DateOnly.FromDateTime(DateTime.Today), "Rate", 1m],
                ["Customer", "Aster Trading", "Lines"],
                ["Item", "Quantity", "Unit price", "Amount"],
                ["Notebook", 10m, 120m],
                ["Ballpoint pen", 20m, 80m]
            ]);
            sheet["C7"].Value = "Subtotal";
            sheet["C8"].Value = "Total (with tax)";

            // Each row reads its own B and C, and every row is calculated again when D1 changes
            sheet.ComputeRows("D4:D6", ["B", "C", "D1"], static x => x.Get<decimal?>("B") * x.Get<decimal?>("C") * x.Get<decimal>("D1"));

            // The watched ranges grow when rows are added inside them
            sheet.Compute("D2", ["A4:A6"], static x => (decimal)x.Values("A4:A6").Count(static v => v is string { Length: > 0 }));
            sheet.Compute("D7", ["D4:D6"], static x => x.Values("D4:D6").OfType<decimal>().Sum());
            sheet.Compute("D8", ["D7"], static x => Math.Round(x.Get<decimal>("D7") * TaxRate, 0, MidpointRounding.AwayFromZero));
        }

        return sheet;
    }

    private static void Label(CellSettings c)
    {
        c.Kind = CellKind.Label;
        c.Style.Bold = true;
        c.Style.FillColor = "#eef2f7";
    }

    private static int FindSubtotalRow(Sheet sheet)
    {
        for (var row = sheet.RowCount - 1; row >= 0; row--)
        {
            if (Equals(sheet.GetValue(row, 2), "Subtotal"))
            {
                return row;
            }
        }

        return sheet.RowCount;
    }

    private void OnRowsInserting(SheetRowsInsertingEventArgs e)
    {
        var subtotal = FindSubtotalRow(e.Sheet);
        if ((e.Index < FirstDetailRow) || (e.Index > subtotal) || (subtotal - FirstDetailRow + e.Count > MaxDetailRows))
        {
            e.Cancel = true;
            log.Add("Insert canceled");
        }
        else
        {
            log.Add($"Insert {e.Count} row(s) at row {e.Index + 1}");
        }
    }

    private void OnRowsDeleting(SheetRowsDeletingEventArgs e)
    {
        var subtotal = FindSubtotalRow(e.Sheet);
        if ((e.Index < FirstDetailRow) || (e.Index + e.Count > subtotal) || (subtotal - FirstDetailRow - e.Count < 1))
        {
            e.Cancel = true;
            log.Add("Delete canceled");
        }
        else
        {
            log.Add($"Delete {e.Count} row(s) at row {e.Index + 1}");
        }
    }
}
