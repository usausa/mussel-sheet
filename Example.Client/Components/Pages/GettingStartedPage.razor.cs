namespace Example.Client.Components.Pages;

public sealed partial class GettingStartedPage
{
    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(5, 4);
        sheet.Columns[0].Header = "Item";
        sheet.Columns[1].Header = "Quantity";
        sheet.Columns[2].Header = "Unit price";
        sheet.Columns[3].Header = "Amount";
        sheet.Columns[0].Width = 160;

        sheet.Range("B:D").Configure(static c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
        });
        sheet.Range("D:D").Configure(static c => c.ReadOnly = true);

        sheet.SetValues(0, 0,
        [
            ["Notebook", 10m, 120m, 1200m],
            ["Ballpoint pen", 20m, 80m, 1600m]
        ]);
        return sheet;
    }

    private void OnCellCommitted(CellCommittedEventArgs e)
    {
        log.OnCellCommitted(e);

        var row = e.Address.Row;
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
