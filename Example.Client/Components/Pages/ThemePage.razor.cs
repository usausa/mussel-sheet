namespace Example.Client.Components.Pages;

public sealed partial class ThemePage
{
    private static readonly SampleTheme[] Themes =
    [
        new("Default", null, null),
        new("Shared variables on a container", "theme-slate", null),
        new("Sheet variables on the sheet", null, "theme-green"),
        new("Dark", null, "theme-dark")
    ];

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private SampleTheme current = Themes[0];

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(8, 2);
        sheet.Columns[0].Header = "Item";
        sheet.Columns[1].Header = "Input";
        sheet.Columns[0].Width = 120;
        sheet.Columns[1].Width = 220;

        sheet.Range("A:A").Configure(static c =>
        {
            c.Kind = CellKind.Label;
            c.Style.CssClass = "theme-label";
        });
        sheet.Range("B1").Configure(static c => c.InputMessage = "A note");
        sheet.Range("B2").Configure(static c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
        });
        sheet.Range("B3").Configure(static c => c.Kind = CellKind.Date);
        sheet.Range("B4").Configure(static c =>
        {
            c.Kind = CellKind.List;
            c.Items = ["Red", "Green", "Blue"];
        });
        sheet.Range("B5").Configure(static c => c.Kind = CellKind.Checkbox);
        sheet.Range("B6").Configure(static c => c.ReadOnly = true);
        sheet.Range("B7").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Medium));

        sheet.SetValues(0, 0,
        [
            ["Text", "Text"],
            ["Number", 1234567m],
            ["Date", new DateOnly(2026, 11, 5)],
            ["List", "Green"],
            ["Checkbox", true],
            ["Read-only", "Read-only"],
            ["Border", "Medium"],
            ["Error", "Error"]
        ]);
        sheet.SetError(7, 1, "An error mark");
        return sheet;
    }

    private sealed record SampleTheme(string Name, string? ContainerClass, string? SheetClass);
}
