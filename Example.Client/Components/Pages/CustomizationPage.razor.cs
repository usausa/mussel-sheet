namespace Example.Client.Components.Pages;

public sealed partial class CustomizationPage
{
    private const string ColorEditor = "color";

    private const string RatingEditor = "rating";

    private const int MaxRating = 5;

    private static readonly string[] OptionFiles = ["SheetOptionsSetup.cs"];

    private static readonly string[] Colors = ["#d93025", "#f29900", "#1e8e3e", "#1a73e8", "#9334e6", "#5f6368"];

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(5, 3);
        sheet.Columns[0].Header = "Item";
        sheet.Columns[1].Header = "Input";
        sheet.Columns[2].Header = "Try";
        sheet.Columns[0].Width = 120;
        sheet.Columns[1].Width = 180;
        sheet.Columns[2].Width = 240;

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
        sheet.Range("B2").Configure(static c =>
        {
            c.Kind = CellKind.Custom;
            c.Editor = ColorEditor;
        });
        sheet.Range("B3").Configure(static c =>
        {
            c.Kind = CellKind.Custom;
            c.Editor = RatingEditor;
        });
        sheet.Range("B4").Configure(static c => c.Required = true);

        sheet.SetValues(0, 0,
        [
            ["Text", "Text", "F4 starts editing"],
            ["Color", "#1a73e8", "F2 opens the editor"],
            ["Rating", 3m, "F2 opens the editor"],
            ["Required", "Text", "Clear it: a registered message"],
            ["Menu", "Text", "Right-click: a registered item"],
        ]);
        return sheet;
    }
}
