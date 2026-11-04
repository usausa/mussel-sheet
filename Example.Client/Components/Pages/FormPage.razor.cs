namespace Example.Client.Components.Pages;

public sealed partial class FormPage
{
    private const int FirstDetailRow = 3;

    private const int MaxDetailRows = 10;

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    protected override void OnInitialized() => sheet.Changed += OnSheetChanged;

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(9, 4);
        string[] headers = ["Item", "Content", "Item", "Content"];
        int[] widths = [100, 140, 100, 140];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Columns[c].Header = headers[c];
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Range("A1:A2").Configure(Label);
            sheet.Range("C1:C2").Configure(Label);
            sheet.Range("A3:D3").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A9:C9").Configure(Label);
            sheet.Range("B1").Configure(static c => c.Kind = CellKind.Date);
            sheet.Range("D1").Configure(static c =>
            {
                c.Kind = CellKind.List;
                c.Items = ["General affairs", "Sales", "Development"];
            });
            sheet.Range("B2").Configure(static c => c.MaxLength = 20);
            sheet.Range("D2").Configure(static c =>
            {
                c.ImeMode = ImeMode.Off;
                c.Pattern = "[0-9]{6}";
                c.ErrorMessage = "Enter an employee number of 6 digits.";
            });
            sheet.Range("B4:C8").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
            });
            sheet.Range("D4:D9").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.ReadOnly = true;
            });

            sheet.Range("A1:D2").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A3:D9").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A1:D2").BorderAround(CellBorder.Medium);
            sheet.Range("A3:D9").BorderAround(CellBorder.Medium);
            sheet.Range("A3:D3").Configure(static c => c.Style.Borders.Bottom = CellBorder.DoubleLine);

            sheet.SetValues(0, 0,
            [
                ["Date", new DateOnly(2026, 11, 1), "Department", "Sales"],
                ["Name", "John Smith", "Employee No.", "A1234"],
                ["Item", "Quantity", "Unit price", "Amount"],
                ["Notebook PC", 2m, 128000m],
                ["Mouse", 5m, 2400m],
                ["Monitor", 2m, 32000m],
            ]);
            sheet["C9"].Value = "Total";
            Calculate(sheet);
            sheet.SetError(1, 3, "Enter an employee number of 6 digits.");
        }

        return sheet;
    }

    private static void Label(CellSettings c)
    {
        c.Kind = CellKind.Label;
        c.Style.Bold = true;
        c.Style.FillColor = "#eef2f7";
    }

    // Rows can be added above the total, so it is searched for
    private static int FindTotalRow(Sheet sheet)
    {
        for (var row = sheet.RowCount - 1; row >= 0; row--)
        {
            if (Equals(sheet.GetValue(row, 2), "Total"))
            {
                return row;
            }
        }

        return sheet.RowCount;
    }

    private static void Calculate(Sheet sheet)
    {
        var totalRow = FindTotalRow(sheet);
        var total = 0m;
        for (var row = FirstDetailRow; row < totalRow; row++)
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

        if (totalRow < sheet.RowCount)
        {
            sheet.SetValue(totalRow, 3, total);
        }
    }

    private void OnCellCommitted(CellCommittedEventArgs e)
    {
        log.OnCellCommitted(e);

        var (row, column) = (e.Address.Row, e.Address.Column);
        if ((row >= FirstDetailRow) && (row < FindTotalRow(e.Sheet)) && (column is 1 or 2))
        {
            Calculate(e.Sheet);
        }
    }

    // Row changes also come from Enter on the last row and from undo, so the total is calculated here
    private void OnSheetChanged(object? sender, SheetChangedEventArgs e)
    {
        if (e.Kind is SheetChangeKind.RowsInserted or SheetChangeKind.RowsDeleted)
        {
            Calculate(sheet);
        }
    }

    private void OnRowsInserting(SheetRowsInsertingEventArgs e)
    {
        var total = FindTotalRow(e.Sheet);
        if ((e.Index < FirstDetailRow) || (e.Index > total))
        {
            e.Cancel = true;
            log.Add("Insert canceled: rows go inside the details");
        }
        else if (total - FirstDetailRow + e.Count > MaxDetailRows)
        {
            e.Cancel = true;
            log.Add($"Insert canceled: the details hold up to {MaxDetailRows} rows");
        }
        else
        {
            log.Add($"Insert {e.Count} row(s) at row {e.Index + 1}");
        }
    }

    private void OnRowsDeleting(SheetRowsDeletingEventArgs e)
    {
        var total = FindTotalRow(e.Sheet);
        if ((e.Index < FirstDetailRow) || (e.Index + e.Count > total))
        {
            e.Cancel = true;
            log.Add("Delete canceled: only detail rows can be deleted");
        }
        else if (total - FirstDetailRow - e.Count < 1)
        {
            e.Cancel = true;
            log.Add("Delete canceled: the details keep at least 1 row");
        }
        else
        {
            log.Add($"Delete {e.Count} row(s) at row {e.Index + 1}");
        }
    }
}
