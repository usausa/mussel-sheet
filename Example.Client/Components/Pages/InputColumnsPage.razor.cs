namespace Example.Client.Components.Pages;

public sealed partial class InputColumnsPage
{
    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        const int Rows = 200;
        string[] headers = ["Product code", "Quantity", "Unit price", "Amount"];

        var sheet = new Sheet(Rows, headers.Length);
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Columns[c].Header = headers[c];
                sheet.Columns[c].Width = 110;
            }

            sheet.Range("A:A").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.Bold = true;
                c.Style.FillColor = "#eef2f7";
            });
            sheet.Range("B:C").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
            });
            sheet.Range("D:D").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.ReadOnly = true;
            });
            sheet.Range("C:C").Configure(static c => c.InputMessage = "Excluding tax");

            for (var r = 0; r < Rows; r++)
            {
                var quantity = (decimal)((r % 7) + 1);
                var price = 100m * ((r % 13) + 1);
                sheet.SetValues(r, 0, [[$"P{r + 1:D4}", quantity, price, quantity * price]]);
            }
        }

        return sheet;
    }

    private void OnCellCommitted(CellCommittedEventArgs e)
    {
        log.OnCellCommitted(e);

        var row = e.Address.Row;
        if (e.Address.Column is 1 or 2)
        {
            if (sheet.TryGetValue<decimal>(row, 1, out var quantity) && sheet.TryGetValue<decimal>(row, 2, out var price))
            {
                sheet.SetValue(row, 3, quantity * price);
            }
            else
            {
                sheet.SetValue(row, 3, null);
            }
        }
    }
}
