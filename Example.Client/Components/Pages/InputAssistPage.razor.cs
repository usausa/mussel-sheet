namespace Example.Client.Components.Pages;

public sealed partial class InputAssistPage
{
    private const int FirstDetailRow = 4;

    private const int TotalRow = 7;

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(8, 6);
        int[] widths = [100, 150, 100, 130, 24, 140];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Range("A2:A3").Configure(Label);
            sheet.Range("C2:C3").Configure(Label);
            sheet.Range("E:E").Configure(Label);
            sheet.Range("A1:F1").Configure(static c =>
            {
                Label(c);
                c.Style.FillColor = "#dbe7f6";
            });
            sheet.Range("A4:D4").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("F4:F8").Configure(Label);

            // Enter and Tab cycle inside the application fields
            sheet.Range("B2:D3").Configure(static c => c.MoveGroup = "application");
            sheet.Range("B2").Configure(static c =>
            {
                c.Kind = CellKind.Date;
                c.Required = true;
                c.InputMessage = "The year can be omitted: 11/5, 1105";
            });
            sheet.Range("D2").Configure(static c =>
            {
                c.Kind = CellKind.List;
                c.Items = ["General affairs", "Sales", "Development"];
                c.InputMessage = "Alt+Down opens the list";
            });
            sheet.Range("B3").Configure(static c => c.MaxLength = 20);
            sheet.Range("D3").Configure(static c =>
            {
                c.Mask = "999999";
                c.ImeMode = ImeMode.Off;
                c.InputMessage = "6 digits";
            });

            // Enter and Tab cycle inside the approval fields
            sheet.Range("F2:F3").Configure(static c => c.MoveGroup = "approval");
            sheet.Range("F3").Configure(static c => c.Kind = CellKind.Date);

            sheet.Range("A5:A7").Configure(static c => c.Kind = CellKind.Date);
            sheet.Range("C5:C7").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
                c.InputMessage = "Including tax";
            });
            sheet.Range("A8:B8").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Right;
            });
            sheet.Range("C8").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.ReadOnly = true;
            });
            sheet.Range("D8").Configure(Label);
            sheet.Range("A1:D8").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("F1:F3").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));

            sheet.SetValues(0, 0,
            [
                ["Expense report", null, null, null, null, "Approval"],
                ["Date", null, "Department"],
                ["Name", null, "Employee No."],
                ["Date", "Description", "Amount", "Notes"]
            ]);
            sheet["A8"].Value = "Total";
            sheet.Range("A1:D1").Merge();
            sheet.Range("A8:B8").Merge();
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
            if (sheet.TryGetValue<decimal>(row, 2, out var amount))
            {
                total += amount;
            }
        }

        sheet.SetValue(TotalRow, 2, total);
    }

    private void OnCellCommitted(CellCommittedEventArgs e)
    {
        log.OnCellCommitted(e);

        if ((e.Address.Row is >= FirstDetailRow and < TotalRow) && (e.Address.Column == 2))
        {
            Calculate(e.Sheet);
        }
    }

    private string ChangedCells()
    {
        var cells = sheet.GetChangedCells();
        return cells.Count == 0 ? "none" : string.Join(" ", cells);
    }

    private void AcceptChanges()
    {
        sheet.AcceptChanges();
        log.Add("Marked as saved");
    }
}
