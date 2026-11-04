namespace Example.Client.Components.Pages;

public sealed partial class SaveAndLoadPage
{
    private readonly SheetEventLog log = new();

    private readonly SheetFunctions functions = CreateFunctions();

    private Sheet sheet = default!;

    private string json = string.Empty;

    protected override void OnInitialized() => sheet = CreateSheet(functions);

    // Functions cannot be saved, so they are registered by name, and the JSON holds the name
    private static SheetFunctions CreateFunctions()
    {
        var functions = new SheetFunctions();
        functions.AddValidator("QuantityLimit", static x => x.Value is > 100m ? "The quantity is up to 100." : null);
        functions.AddFormatter("Dollars", static x => x.Value is null ? null : $"${x.Text}");
        functions.AddComputation("Amount", static x => x.Get<decimal?>("B") * x.Get<decimal?>("C"));
        functions.AddComputation("Total", static x => x.Values("D4:D6").OfType<decimal>().Sum());
        return functions;
    }

    private static Sheet CreateSheet(SheetFunctions functions)
    {
        var sheet = new Sheet(8, 4) { FrozenRows = 3 };
        int[] widths = [120, 90, 110, 140];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Rows[0].Height = 40;
            sheet.Rows[7].Height = 48;
            sheet.Range("A1:D1").Merge();
            sheet.Range("A1:D1").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.Bold = true;
                c.Style.FontSize = 20;
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A2").Configure(Label);
            sheet.Range("C2").Configure(Label);
            sheet.Range("A3:D3").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A7:C7").Configure(Label);
            sheet.Range("A8").Configure(Label);
            sheet.Range("B2").Configure(static c => c.Kind = CellKind.Date);
            sheet.Range("D2").Configure(static c =>
            {
                c.Required = true;
                c.InputMessage = "The name of the customer";
            });
            sheet.Range("A4:A6").Configure(static c => c.MaxLength = 20);
            sheet.Range("B4:C6").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
            });
            sheet.Range("B4:B6").Configure(c => c.Validator = functions.Validator("QuantityLimit"));
            sheet.Range("D4:D7").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
            });
            sheet.Range("D7").Configure(c =>
            {
                c.Style.Bold = true;
                c.Formatter = functions.Formatter("Dollars");
            });
            sheet.Range("B8:D8").Merge();
            sheet.Range("B8").Configure(static c =>
            {
                c.WrapText = true;
                c.Style.VerticalAlignment = VerticalAlignment.Top;
            });
            sheet.Range("A2:D8").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A2:D8").BorderAround(CellBorder.Medium);
            sheet.SetValues(0, 0,
            [
                ["Delivery note"],
                ["Date", DateOnly.FromDateTime(DateTime.Today), "Customer", "Aster Trading"],
                ["Item", "Quantity", "Unit price", "Amount"],
                ["Notebook", 10m, 120m],
                ["Ballpoint pen", 20m, 80m],
            ]);
            sheet["C7"].Value = "Total";
            sheet["A8"].Value = "Notes";

            sheet.ComputeRows("D4:D6", ["B", "C"], functions.Computation("Amount"));
            sheet.Compute("D7", ["D4:D6"], functions.Computation("Total"));
        }

        return sheet;
    }

    private static void Label(CellSettings c)
    {
        c.Kind = CellKind.Label;
        c.Style.Bold = true;
        c.Style.FillColor = "#eef2f7";
    }

    private void Save(bool includeValues)
    {
        json = SheetDesignJson.Write(sheet.ToDesign(functions, includeValues));
        log.Add(includeValues ? "Saved with the values" : "Saved the design");
    }

    private void Load()
    {
        sheet = Sheet.FromDesign(SheetDesignJson.Read(json), functions);
        log.Add("Loaded a new sheet from the JSON");
    }
}
