namespace Example.Client.Components.Pages;

using Microsoft.AspNetCore.Components;

public sealed partial class LargeSheetsPage
{
    private const int Columns = 50;

    private const int TallRowInterval = 50;

    private readonly SheetEventLog log = new();

    private Sheet sheet = default!;

    private int created;

    [SupplyParameterFromQuery]
    public int? Rows { get; set; }

    protected override void OnParametersSet()
    {
        var rows = Rows is 10000 ? 10000 : 1000;
        if (created != rows)
        {
            created = rows;
            sheet = CreateSheet(rows);
        }
    }

    private static Sheet CreateSheet(int rows)
    {
        var sheet = new Sheet(rows, Columns) { FrozenRows = 1, FrozenColumns = 1 };
        using (sheet.BeginUpdate())
        {
            sheet.Columns[0].Width = 120;
            sheet.Range("A:A").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.Bold = true;
                c.Style.FillColor = "#eef2f7";
            });
            sheet.Range("B:AX").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
            });
            for (var c = 5; c < Columns; c += 5)
            {
                sheet.Range(CellRange.Columns(c, c)).Configure(static x => x.Style.FillColor = "#fffbe6");
            }

            sheet.Range("B2:F6").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("B2:F6").BorderAround(CellBorder.Thick.WithColor("#1a73e8"));
            sheet.Range("AA100:AD105").Merge();

            for (var r = 0; r < rows; r++)
            {
                var values = new object?[Columns];
                values[0] = $"Row {r + 1}";
                for (var c = 1; c < Columns; c++)
                {
                    values[c] = (decimal)((r + 1) * c);
                }

                sheet.SetValues(r, 0, [values]);
                if (r % TallRowInterval == TallRowInterval - 1)
                {
                    sheet.Rows[r].Height = 40;
                }
            }

            sheet.SetError(2, 2, "An error mark");
        }

        return sheet;
    }
}
