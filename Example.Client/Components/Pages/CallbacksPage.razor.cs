namespace Example.Client.Components.Pages;

public sealed partial class CallbacksPage
{
    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(8, 3);
        int[] widths = [120, 160, 440];
        var today = DateOnly.FromDateTime(DateTime.Today);
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Range("A1:C1").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A2:A8").Configure(Label);
            sheet.Range("C2:C8").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.FontColor = "#57606a";
            });

            sheet.Range("B2").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Maximum = 100000;
                c.Corrector = static x => x.Value is decimal d ? Math.Round(d, 0, MidpointRounding.AwayFromZero) : x.Value;
                c.Formatter = static x => x.Value is null ? null : $"${x.Text}";
            });
            sheet.Range("B3").Configure(static c =>
            {
                c.Pattern = "[A-Z0-9-]+";
                c.Corrector = static x => (x.Value as string)?.ToUpperInvariant();
            });
            sheet.Range("B4").Configure(static c => c.Formatter = static x => x.Value switch
            {
                "0" => "Pending",
                "1" => "Approved",
                "2" => "Returned",
                _ => null
            });
            sheet.Range("B5").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.StyleProvider = static x => x.Value switch
                {
                    < 0m => new CellStyle { FontColor = "#c62828" },
                    > 100000m => new CellStyle { Bold = true },
                    _ => null
                };
            });
            sheet.Range("B6").Configure(c =>
            {
                c.Kind = CellKind.Date;
                c.StyleProvider = x => x.Value is DateOnly date && (date < today) ? new CellStyle { FillColor = "#fff3cd", Bold = true } : null;
            });
            sheet.Range("B7").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Formatter = static x => x.Value is decimal d ? d.ToString("P0", x.Sheet.Culture) : null;
                c.StyleProvider = static x => x.Value is >= 0.8m ? new CellStyle { FontColor = "#1e8e3e", Bold = true } : null;
            });
            sheet.Range("B8").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Validator = static x => x.Value is decimal d && (d % 2 != 0) ? "Enter an even number." : null;
            });

            sheet.SetValues(0, 0,
            [
                ["Item", "Input", "Function"],
                ["Amount", 12800m, "Corrector: rounds, before Maximum is checked. Formatter: adds $"],
                ["Code", "AB-001", "Corrector: upper-cases, before Pattern is checked"],
                ["Status", "1", "Formatter: 0 = Pending, 1 = Approved, 2 = Returned"],
                ["Profit", -3500m, "StyleProvider: red below 0, bold above 100,000"],
                ["Due date", today.AddDays(-3), "StyleProvider: yellow before today"],
                ["Progress", 0.85m, "Formatter: percent. StyleProvider: green from 80%"],
                ["Even number", 4m, "Validator: rejects odd numbers"]
            ]);
        }

        return sheet;
    }

    private static void Label(CellSettings c)
    {
        c.Kind = CellKind.Label;
        c.Style.Bold = true;
        c.Style.FillColor = "#eef2f7";
    }
}
