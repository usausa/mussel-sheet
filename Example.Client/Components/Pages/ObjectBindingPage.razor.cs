namespace Example.Client.Components.Pages;

using System.Text;

using Example.Client.Models;

public sealed partial class ObjectBindingPage
{
    private const int FirstDetailRow = 3;

    private const int MaxDetailRows = 10;

    private static readonly string[] ModelFiles = ["Order.cs", "OrderLine.cs"];

    private static readonly string[] Customers = ["Aster Trading", "Birch Printing", "Clover Foods"];

    private readonly SheetEventLog log = new();

    private readonly Order order = new() { OrderDate = new DateOnly(2026, 11, 3), Customer = Customers[0], Department = "Sales" };

    private readonly Sheet sheet = CreateSheet();

    private int added;

    protected override void OnInitialized()
    {
        order.Lines.Add(new OrderLine { Name = "Notebook", Quantity = 10m, Price = 120m });
        order.Lines.Add(new OrderLine { Name = "Ballpoint pen", Quantity = 20m, Price = 80m });

        // Rows 4 to 6 are the details. A row without an item stays empty until a value is typed
        sheet.Bind(order)
            .Cell("B1", static x => x.OrderDate)
            .Cell("D1", static x => x.Customer)
            .Cell("B2", static x => x.Department)
            .Cell("D2", static x => x.LineCount)
            .Cell("D7", static x => x.Total)
            .Rows("A4:D6", static x => x.Lines, static row => row
                .Column("A", static x => x.Name)
                .Column("B", static x => x.Quantity)
                .Column("C", static x => x.Price)
                .Column("D", static x => x.Amount));

        sheet.Changed += (_, _) => _ = InvokeAsync(StateHasChanged);
    }

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(7, 4);
        int[] widths = [100, 140, 100, 140];
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
            sheet.Range("A7:C7").Configure(Label);
            sheet.Range("B1").Configure(static c => c.Kind = CellKind.Date);
            sheet.Range("B2").Configure(static c =>
            {
                c.Kind = CellKind.List;
                c.Items = ["General affairs", "Sales", "Development"];
            });
            sheet.Range("B4:C6").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
            });
            sheet.Range("D4:D7").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.ReadOnly = true;
            });
            sheet.Range("D2").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.ReadOnly = true;
            });
            sheet.Range("A1:D2").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A3:D7").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A1:D2").BorderAround(CellBorder.Medium);
            sheet.Range("A3:D7").BorderAround(CellBorder.Medium);
            sheet.Range("A3:D3").Configure(static c => c.Style.Borders.Bottom = CellBorder.DoubleLine);
            sheet.SetValues(0, 0,
            [
                ["Order date", null, "Customer"],
                ["Department", null, "Lines"],
                ["Item", "Quantity", "Unit price", "Amount"]
            ]);
            sheet["C7"].Value = "Total";
        }

        return sheet;
    }

    private static void Label(CellSettings c)
    {
        c.Kind = CellKind.Label;
        c.Style.Bold = true;
        c.Style.FillColor = "#eef2f7";
    }

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

    private void AddLine() => order.Lines.Add(new OrderLine { Name = $"Added line {++added}", Quantity = 1m, Price = 100m });

    private void RemoveFirstLine()
    {
        if (order.Lines.Count > 0)
        {
            order.Lines.RemoveAt(0);
        }
    }

    private void ChangeCustomer() => order.Customer = Customers[(Array.IndexOf(Customers, order.Customer) + 1) % Customers.Length];

    private void OnRowsInserting(SheetRowsInsertingEventArgs e)
    {
        var total = FindTotalRow(e.Sheet);
        if ((e.Index < FirstDetailRow) || (e.Index > total) || (total - FirstDetailRow + e.Count > MaxDetailRows))
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
        var total = FindTotalRow(e.Sheet);
        if ((e.Index < FirstDetailRow) || (e.Index + e.Count > total) || (total - FirstDetailRow - e.Count < 1))
        {
            e.Cancel = true;
            log.Add("Delete canceled");
        }
        else
        {
            log.Add($"Delete {e.Count} row(s) at row {e.Index + 1}");
        }
    }

    private string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Order date: {order.OrderDate} / Customer: {order.Customer} / Department: {order.Department}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Lines: {order.LineCount} / Total: {order.Total:N0}");
        for (var i = 0; i < order.Lines.Count; i++)
        {
            var line = order.Lines[i];
            text.AppendLine(CultureInfo.InvariantCulture, $"{i + 1}. {line.Name} / Quantity {line.Quantity:N0} / Unit price {line.Price:N0} / Amount {line.Amount:N0}");
        }

        return text.ToString();
    }
}
