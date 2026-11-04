namespace Example.Client.Components.Pages;

public sealed partial class CellKindsPage
{
    private static readonly string[] Customers =
    [
        "Acme Corporation",
        "Alder Logistics",
        "Aster Trading",
        "Birch Printing",
        "Cedar Construction",
        "Clover Foods",
        "Elm Insurance",
        "Fern Chemicals",
        "Hazel Books",
        "Maple Electronics",
        "Oak Bank",
        "Pine Transport",
        "Rowan Pharma",
        "Willow Design",
    ];

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        (string Label, string Setting)[] rows =
        [
            ("Text", "MaxLength = 10"),
            ("Required text", "Required"),
            ("Number", "Minimum = 0, Maximum = 100"),
            ("Amount", "Format = N0"),
            ("Date", "Minimum = 1/1/2026"),
            ("Time", "CellKind.Time"),
            ("Checkbox", "CellKind.Checkbox"),
            ("Department", "CellKind.List"),
            ("Postal code", "Pattern = [0-9]{3}-[0-9]{4}"),
            ("Employee code", "MaxByteLength = 8"),
            ("Read-only", "ReadOnly"),
            ("Accept and mark", "ValidationMode = AcceptAndMark"),
            ("Revert", "ValidationMode = Revert"),
            ("Phone", "Mask = 999-9999-9999"),
            ("Product code", "CharacterConversions, CharacterRestriction"),
            ("Customer", "SuggestionProvider"),
            ("Done", "CheckedText, UncheckedText"),
            ("Notes", "WrapText"),
        ];

        var sheet = new Sheet(rows.Length, 3);
        using (sheet.BeginUpdate())
        {
            sheet.Columns[0].Header = "Item";
            sheet.Columns[1].Header = "Input";
            sheet.Columns[2].Header = "Setting";
            sheet.Columns[0].Width = 140;
            sheet.Columns[1].Width = 200;
            sheet.Columns[2].Width = 300;
            sheet.Range("A:A").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.Bold = true;
                c.Style.FillColor = "#eef2f7";
            });
            sheet.Range("C:C").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.FontColor = "#57606a";
            });
            sheet.Range("A:C").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            for (var r = 0; r < rows.Length; r++)
            {
                sheet.SetValue(r, 0, rows[r].Label);
                sheet.SetValue(r, 2, rows[r].Setting);
            }

            sheet.Range("B1").Configure(static c => c.MaxLength = 10);
            sheet.Range("B2").Configure(static c => c.Required = true);
            sheet.Range("B3").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Minimum = 0;
                c.Maximum = 100;
            });
            sheet.Range("B4").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
            });
            sheet.Range("B5").Configure(static c =>
            {
                c.Kind = CellKind.Date;
                c.Minimum = new DateOnly(2026, 1, 1);
            });
            sheet.Range("B6").Configure(static c => c.Kind = CellKind.Time);
            sheet.Range("B7").Configure(static c => c.Kind = CellKind.Checkbox);
            sheet.Range("B8").Configure(static c =>
            {
                c.Kind = CellKind.List;
                c.Items = [new CellListItem("G", "General affairs"), new CellListItem("S", "Sales"), new CellListItem("D", "Development")];
            });
            sheet.Range("B9").Configure(static c =>
            {
                c.Pattern = "[0-9]{3}-[0-9]{4}";
                c.ErrorMessage = "Enter a postal code in the form 123-4567.";
                c.ImeMode = ImeMode.Off;
            });
            sheet.Range("B10").Configure(static c => c.MaxByteLength = 8);
            sheet.Range("B11").Configure(static c => c.ReadOnly = true);
            sheet.Range("B12").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.ValidationMode = SheetValidationMode.AcceptAndMark;
            });
            sheet.Range("B13").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.ValidationMode = SheetValidationMode.Revert;
            });
            sheet.Range("B14").Configure(static c =>
            {
                c.Kind = CellKind.Mask;
                c.Mask = "999-9999-9999";
                c.ImeMode = ImeMode.Off;
            });
            sheet.Range("B15").Configure(static c =>
            {
                c.CharacterConversions = CellCharacterConversions.FullwidthAsciiToHalfwidth;
                c.CharacterRestriction = CellCharacterRestriction.Alphanumeric;
                c.ImeMode = ImeMode.Off;
            });
            sheet.Range("B16").Configure(static c =>
            {
                c.Kind = CellKind.AutoComplete;
                c.SuggestionProvider = SuggestCustomersAsync;
            });
            sheet.Range("B17").Configure(static c =>
            {
                c.Kind = CellKind.Checkbox;
                c.CheckedText = "Yes";
                c.UncheckedText = "No";
            });
            sheet.Range("B18").Configure(static c => c.WrapText = true);
            sheet.Rows[17].Height = 60;

            sheet["B4"].Value = 1234567m;
            sheet["B5"].Value = new DateOnly(2026, 11, 2);
            sheet["B6"].Value = new TimeOnly(9, 30);
            sheet["B7"].Value = false;
            sheet["B8"].Value = "S";
            sheet["B11"].Value = "Read-only";
            sheet["B18"].Value = "First line\nThe second line is long and wraps to the width of the cell";
        }

        return sheet;
    }

    // A real app asks a server or a database here. Typing on cancels the previous call
    private static async Task<IReadOnlyList<string>> SuggestCustomersAsync(CellSuggestionContext context)
    {
        await Task.Delay(300, context.CancellationToken);
        var text = context.Text.Trim();
        return [.. Customers.Where(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))];
    }
}
