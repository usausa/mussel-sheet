namespace MusselSheet;

using Microsoft.AspNetCore.Components;

using MusselSheet.Internal;

public sealed class CellCallbackTests : BunitContext
{
    public CellCallbackTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task TheValidatorAndCommittingGetTheCorrectedText()
    {
        string? validated = null;
        string? committing = null;
        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(c =>
        {
            c.Corrector = static x => x.Value is string text ? text.ToUpperInvariant() : x.Value;
            c.Validator = x =>
            {
                validated = x.Text;
                return null;
            };
        });
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.CellCommitting, e => committing = e.Text));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "abc", false));

        Assert.Equal("ABC", sheet.GetValue(0, 0));
        Assert.Equal("ABC", validated);
        Assert.Equal("ABC", committing);
    }

    [Fact]
    public async Task CorrectorRunsBeforeTheRules()
    {
        var sheet = new Sheet(3, 1);
        sheet.Range("A1:A2").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Maximum = 1000;
            c.Corrector = x => x.Value is decimal d ? Math.Min(Math.Round(d, 2, MidpointRounding.AwayFromZero), 1000m) : x.Value;
        });
        sheet.Range("A3").Configure(c =>
        {
            c.Pattern = "[A-Z]+";
            c.Corrector = x => (x.Value as string)?.ToUpperInvariant();
        });
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "12.345", false));
        var clamped = await cut.InvokeAsync(() => cut.Instance.CommitAsync(1, 0, "2000", false));
        var upper = await cut.InvokeAsync(() => cut.Instance.CommitAsync(2, 0, "abc", false));

        Assert.Equal(12.35m, sheet.GetValue(0, 0));
        Assert.True(clamped.Ok);
        Assert.Equal(1000m, sheet.GetValue(1, 0));
        Assert.True(upper.Ok);
        Assert.Equal("ABC", sheet.GetValue(2, 0));

        Assert.Null(sheet.GetInputText(0, 0));
        Assert.Equal("12.35", sheet.GetEditText(0, 0));
    }

    [Fact]
    public async Task CorrectorWorksForEveryUserInputButNotForTheApp()
    {
        var sheet = new Sheet(4, 1);
        sheet.Range("A:A").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Corrector = x => x.Value is decimal d ? Math.Round(d, 0) : 0m;
        });
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.PasteTextAsync(new CellRange(0, 0, 0, 0), "1.4"));
        await cut.InvokeAsync(() => cut.Instance.FillDownAsync(0, 0, 1, 0));
        Assert.Equal(1m, sheet.GetValue(0, 0));
        Assert.Equal(1m, sheet.GetValue(1, 0));

        sheet.SetValue(2, 0, 2.6m);
        Assert.Equal(2.6m, sheet.GetValue(2, 0));
        await cut.InvokeAsync(() => cut.Instance.ClearAsync(1, 0));
        Assert.Null(sheet.GetValue(1, 0));
        await cut.InvokeAsync(cut.Instance.UndoAsync);
        Assert.Equal(1m, sheet.GetValue(1, 0));
    }

    [Fact]
    public async Task CustomEditorValuesAreCorrected()
    {
        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(c =>
        {
            c.Kind = CellKind.Custom;
            c.Editor = "color";
            c.Corrector = x => (x.Value as string)?.ToUpperInvariant();
        });
        CellEditContext? opened = null;
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CustomEditor, Editor));

        await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 0));
        opened!.Value = "#00ff00";
        await cut.InvokeAsync(() => cut.Instance.CommitCustomEditorAsync());

        Assert.Equal("#00FF00", sheet.GetValue(0, 0));

        RenderFragment Editor(CellEditContext context) => builder =>
        {
            opened = context;
            builder.AddContent(0, context.Name);
        };
    }

    [Fact]
    public void FormatterChangesOnlyTheDisplayText()
    {
        var sheet = new Sheet(2, 2);
        sheet.Range("A:A").Configure(c => c.Formatter = x => x.Value switch
        {
            "1" => "承認済",
            "0" => "未承認",
            _ => null
        });
        sheet.Range("B1").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
            c.Formatter = x => $"{x.Text} 円";
        });
        sheet.SetValues(0, 0, [["1", 1200m], ["x"]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var a1 = cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']");
        Assert.Equal("承認済", a1.TextContent);
        Assert.Equal("1", a1.GetAttribute("data-edit"));
        Assert.Equal("1,200 円", cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='1']").TextContent);
        Assert.Equal("x", cut.Find(".mussel-sheet-row[data-r='1'] .mussel-sheet-cell[data-c='0']").TextContent);
        Assert.Equal("承認済", sheet.GetText(0, 0));
        Assert.Equal("1", sheet.GetEditText(0, 0));
    }

    [Fact]
    public void StyleProviderOverlaysTheCellStyle()
    {
        var sheet = new Sheet(4, 1);
        sheet.Range("A:A").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Style.Bold = true;
            c.Style.Underline = true;
            c.StyleProvider = x => x.Value switch
            {
                < 0m => new CellStyle { FontColor = "#c62828", Bold = false, Strikethrough = true, IndentLevel = 1, CssClass = "minus" },
                > 1000m => new CellStyle { FillColor = "red;display:none" },
                _ => null
            };
        });

        sheet.Range("A1").Configure(c => c.Formatter = x => $"({x.Text})");
        sheet.SetValues(0, 0, [[-5m], [10m], [5000m]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var minus = cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']");
        Assert.Equal("(-5)", minus.TextContent);
        Assert.Contains("minus", minus.ClassList);
        Assert.Equal("font-weight:400;text-decoration-line:underline line-through;padding-right:calc(4px + 1em);color:#c62828;", minus.GetAttribute("style"));

        Assert.Null(cut.Find(".mussel-sheet-row[data-r='1'] .mussel-sheet-cell[data-c='0']").GetAttribute("style"));
        Assert.Null(cut.Find(".mussel-sheet-row[data-r='2'] .mussel-sheet-cell[data-c='0']").GetAttribute("style"));
    }

    [Fact]
    public void CorrectedCellsAreCheckedOnlyForTheirTypeInJavaScript()
    {
        var sheet = new Sheet(1, 2);
        sheet.Range("A1").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Corrector = x => x.Value;
        });

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);
        var pattern = map.Patterns[map.RowPatterns[0]];
        Assert.True(map.Rules[pattern[0]].Corrects);
        Assert.False(map.Rules[pattern[1]].Corrects);
    }
}
