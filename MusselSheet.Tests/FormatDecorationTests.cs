namespace MusselSheet;

using Bunit;

using MusselSheet.Internal;

public sealed class FormatDecorationTests : BunitContext
{
    public FormatDecorationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void DecorationsAreResolvedItemByItem()
    {
        var sheet = new Sheet(2, 2);
        sheet.Range("A1:B2").Configure(c =>
        {
            c.Style.Italic = true;
            c.Style.Underline = true;
            c.Style.FontSize = 20;
            c.Style.IndentLevel = 2;
        });

        sheet.Range("A1").Configure(c =>
        {
            c.Style.Italic = false;
            c.Style.Strikethrough = true;
        });

        var a1 = sheet.GetSettings(0, 0).Style;
        Assert.False(a1.Italic);
        Assert.True(a1.Underline);
        Assert.True(a1.Strikethrough);
        Assert.Equal(20, a1.FontSize);
        Assert.Equal(2, a1.IndentLevel);
        Assert.True(sheet.GetSettings(1, 1).Style.Italic);
        Assert.Null(new Sheet(1, 1).GetSettings(0, 0).Style.FontSize);

        var style = new CellStyle();
        Assert.Throws<ArgumentOutOfRangeException>(() => style.FontSize = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => style.IndentLevel = -1);
    }

    [Fact]
    public void DecorationsBecomeCellClasses()
    {
        var sheet = new Sheet(1, 4);
        sheet.Range("A1").Configure(c =>
        {
            c.Style.Italic = true;
            c.Style.Underline = true;
            c.Style.Strikethrough = true;
            c.Style.FontSize = 20;
            c.Style.IndentLevel = 2;
        });
        sheet.Range("B1").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Style.IndentLevel = 1;
        });
        sheet.Range("C1").Configure(c =>
        {
            c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            c.Style.IndentLevel = 1;
        });

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var css = cut.Find("style").TextContent;
        var cells = cut.FindAll(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell");

        Assert.Subset(cells[0].ClassList.ToHashSet(), new HashSet<string> { "mussel-sheet-i", "mussel-sheet-u", "mussel-sheet-s", "fs20", "il2" });
        Assert.Contains(".fs20{font-size:20px}", css, StringComparison.Ordinal);
        Assert.Contains(".il2{padding-left:calc(4px + 2em)}", css, StringComparison.Ordinal);

        Assert.Contains("ir1", cells[1].ClassList);
        Assert.DoesNotContain(cells[2].ClassList, x => x.StartsWith("il", StringComparison.Ordinal) || x.StartsWith("ir", StringComparison.Ordinal));
        Assert.DoesNotContain(cells[3].ClassList, x => x is "mussel-sheet-i" or "mussel-sheet-u" or "mussel-sheet-s" || x.StartsWith("fs", StringComparison.Ordinal));
    }

    [Fact]
    public void TheEditorGetsTheCellFont()
    {
        var sheet = new Sheet(1, 2);
        sheet.Range("A1").Configure(c =>
        {
            c.Style.Bold = true;
            c.Style.Italic = true;
            c.Style.Strikethrough = true;
            c.Style.FontSize = 16;
            c.Style.FontColor = "#c62828";
        });

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);
        var pattern = map.Patterns[map.RowPatterns[0]];
        Assert.Equal(new EditorFont(true, true, false, true, 16, "#c62828"), map.Rules[pattern[0]].Font);
        Assert.Null(map.Rules[pattern[1]].Font);
    }
}
