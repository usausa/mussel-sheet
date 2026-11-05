namespace Example.Client.Components.Pages;

public sealed partial class MergedCellsPage
{
    private const int FirstDetailRow = 4;

    private const int TotalRow = 7;

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(11, 4);
        int[] widths = [100, 120, 120, 120];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Rows[0].Height = 36;
            sheet.Range("A1:D1").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
                c.Style.FillColor = "#dbe7f6";
            });
            sheet.Range("A2:A3").Configure(Label);
            sheet.Range("B2:D3").Configure(static c => c.MaxLength = 30);
            sheet.Range("A4:D4").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("B5:C7").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
            });
            sheet.Range("D5:D8").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.ReadOnly = true;
            });
            sheet.Range("A8:C8").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Right;
            });
            sheet.Range("A9:A11").Configure(Label);
            sheet.Range("B9:D11").Configure(static c =>
            {
                c.WrapText = true;
                c.Style.VerticalAlignment = VerticalAlignment.Top;
            });
            sheet.Range("A1:D11").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A1:D11").BorderAround(CellBorder.Medium);

            sheet.SetValues(0, 0,
            [
                ["Quotation"],
                ["To", "Sample Trading Co."],
                ["Subject", "Office supplies"],
                ["Item", "Quantity", "Unit price", "Amount"],
                ["Copy paper", 10m, 450m],
                ["Ballpoint pen", 50m, 120m]
            ]);
            sheet["A8"].Value = "Total";
            sheet["A9"].Value = "Notes";
            sheet["B9"].Value = "Delivery: one week after the order\nValid for 30 days";

            foreach (var range in new[] { "A1:D1", "B2:D2", "B3:D3", "A8:C8", "A9:A11", "B9:D11" })
            {
                sheet.Range(range).Merge();
            }

            Calculate(sheet);
        }

        return sheet;
    }

    private static void Label(CellSettings c)
    {
        c.Kind = CellKind.Label;
        c.Style.Bold = true;
        c.Style.FillColor = "#eef2f7";
    }

    private static void Calculate(Sheet sheet)
    {
        var total = 0m;
        for (var row = FirstDetailRow; row < TotalRow; row++)
        {
            if (sheet.TryGetValue<decimal>(row, 1, out var quantity) && sheet.TryGetValue<decimal>(row, 2, out var price))
            {
                sheet.SetValue(row, 3, quantity * price);
                total += quantity * price;
            }
            else
            {
                sheet.SetValue(row, 3, null);
            }
        }

        sheet.SetValue(TotalRow, 3, total);
    }

    private void OnCellCommitted(CellCommittedEventArgs e)
    {
        log.OnCellCommitted(e);

        if ((e.Address.Row is >= FirstDetailRow and < TotalRow) && (e.Address.Column is 1 or 2))
        {
            Calculate(e.Sheet);
        }
    }
}
