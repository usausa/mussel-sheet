namespace Example.Client.Components.Pages;

public sealed partial class FormattingPage
{
    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(17, 3);
        int[] widths = [120, 200, 240];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Range("A1:C1").Merge();
            sheet.Range("A1").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.FontSize = 20;
                c.Style.Bold = true;
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Rows[0].Height = 40;
            sheet.Range("A2:C2").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A3:A17").Configure(Label);
            sheet.Range("C3:C17").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.FontColor = "#57606a";
            });

            // A right-aligned value is indented from the right
            sheet.Range("A5:A6").Configure(static c =>
            {
                c.Style.Bold = false;
                c.Style.IndentLevel = 1;
            });
            sheet.Range("B3").Configure(static c =>
            {
                c.Style.FontSize = 16;
                c.Style.Bold = true;
            });
            sheet.Range("B5:B8").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
            });
            sheet.Range("B6").Configure(static c => c.Style.IndentLevel = 2);
            sheet.Range("A7:B7").Configure(static c => c.Style.Strikethrough = true);
            sheet.Range("A8:B8").Configure(static c =>
            {
                c.Style.Underline = true;
                c.Style.Bold = true;
            });
            sheet.Range("B8").Configure(static c => c.ReadOnly = true);
            sheet.Range("B9").Configure(static c =>
            {
                c.Style.Italic = true;
                c.Style.FontColor = "#1a73e8";
            });
            sheet.Range("B10").Configure(static c =>
            {
                c.Style.Italic = true;
                c.Style.Underline = true;
                c.Style.Strikethrough = true;
            });
            sheet.Range("B11").Configure(static c => c.Style.FillColor = "#fff3cd");

            // Only the bottom border is set, so the lines of neighboring rows do not overlap
            sheet.Range("B12").Configure(static c => c.Style.Borders.Bottom = CellBorder.Thin);
            sheet.Range("B13").Configure(static c => c.Style.Borders.Bottom = CellBorder.Medium);
            sheet.Range("B14").Configure(static c => c.Style.Borders.Bottom = CellBorder.Thick);
            sheet.Range("B15").Configure(static c => c.Style.Borders.Bottom = CellBorder.Dashed);
            sheet.Range("B16").Configure(static c => c.Style.Borders.Bottom = CellBorder.Dotted);
            sheet.Range("B17").Configure(static c => c.Style.Borders.Bottom = CellBorder.DoubleLine.WithColor("#d93025"));

            sheet.SetValues(0, 0,
            [
                ["Expense report"],
                ["Item", "Input", "Style"],
                ["Subject", "November trip", "FontSize = 16, Bold"],
                ["Expenses", null, null],
                ["Transport", 12800m, "IndentLevel = 1 on the label"],
                ["Hotel", 9800m, "IndentLevel = 2 on the amount"],
                ["Old rate", 15000m, "Strikethrough"],
                ["Total", 22600m, "Underline, Bold"],
                ["Notes", "Receipts sent separately", "Italic, FontColor"],
                ["Memo", "Three decorations", "Italic, Underline, Strikethrough"],
                ["Fill", "A yellow cell", "FillColor"],
                ["Thin", null, "CellBorder.Thin"],
                ["Medium", null, "CellBorder.Medium"],
                ["Thick", null, "CellBorder.Thick"],
                ["Dashed", null, "CellBorder.Dashed"],
                ["Dotted", null, "CellBorder.Dotted"],
                ["Double", null, "CellBorder.DoubleLine.WithColor"],
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
