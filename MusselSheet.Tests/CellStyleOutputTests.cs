namespace MusselSheet;

using Bunit;

using MusselSheet.Internal;

public sealed class CellStyleOutputTests : BunitContext
{
    public CellStyleOutputTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public static TheoryData<string, CellStyle, string> ValueStyles => new()
    {
        { "center", new CellStyle { HorizontalAlignment = HorizontalAlignment.Center }, "text-align:center;" },
        { "right", new CellStyle { HorizontalAlignment = HorizontalAlignment.Right }, "text-align:right;" },
        { "left", new CellStyle { HorizontalAlignment = HorizontalAlignment.Left }, "text-align:left;" },
        { "bold", new CellStyle { Bold = true }, "font-weight:600;" },
        { "not bold", new CellStyle { Bold = false }, "font-weight:400;" },
        { "italic", new CellStyle { Italic = true }, "font-style:italic;" },
        { "not italic", new CellStyle { Italic = false }, "font-style:normal;" },
        { "underline", new CellStyle { Underline = true }, "text-decoration-line:underline;" },
        { "strikethrough", new CellStyle { Strikethrough = true }, "text-decoration-line:line-through;" },
        { "both lines", new CellStyle { Underline = true, Strikethrough = true }, "text-decoration-line:underline line-through;" },
        { "no lines", new CellStyle { Underline = false }, "text-decoration-line:none;" },
        { "size", new CellStyle { FontSize = 16 }, "font-size:16px;" },
        { "indent", new CellStyle { IndentLevel = 2 }, "padding-left:calc(4px + 2em);" },
        { "indent from the right", new CellStyle { HorizontalAlignment = HorizontalAlignment.Right, IndentLevel = 1 }, "text-align:right;padding-right:calc(4px + 1em);" },
        { "no indent when centered", new CellStyle { HorizontalAlignment = HorizontalAlignment.Center, IndentLevel = 1 }, "text-align:center;" }
    };

    [Theory]
    [MemberData(nameof(ValueStyles))]
    public void StyleByValueBecomesAnInlineStyle(string name, CellStyle patch, string expected)
    {
        Assert.NotNull(name);
        Assert.Equal(expected, SheetStyles.GetValueStyle(new Sheet(1, 1).GetSettings(0, 0), patch));
    }

    public static TheoryData<CellBorder, string> Borders => new()
    {
        { CellBorder.Thin, "thin" },
        { CellBorder.Medium, "medium" },
        { CellBorder.Thick, "thick" },
        { CellBorder.Dashed, "dashed" },
        { CellBorder.Dotted, "dotted" },
        { CellBorder.DoubleLine, "double" }
    };

    [Theory]
    [MemberData(nameof(Borders))]
    public void EveryLineStyleHasItsClass(CellBorder line, string expected)
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.Style.Borders = new CellBorders { Bottom = line });

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Contains($"mussel-sheet-bb-{expected}", cut.Find(".mussel-sheet-cell").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void NoLineStyleHasNoClass()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(static c => c.Style.Borders = new CellBorders { Bottom = CellBorder.None });

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.DoesNotContain("mussel-sheet-bb-", cut.Find(".mussel-sheet-cell").ClassName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VerticalAlignment.Top, "mussel-sheet-va-t")]
    [InlineData(VerticalAlignment.Bottom, "mussel-sheet-va-b")]
    public void VerticalAlignmentHasItsClass(VerticalAlignment alignment, string expected)
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.Style.VerticalAlignment = alignment);

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Contains(expected, cut.Find(".mussel-sheet-cell").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void CssClassGoesOnTheCell()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(static c => c.Style.CssClass = " my-cell ");

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        Assert.Contains(" my-cell", cut.Find(".mussel-sheet-cell").ClassName, StringComparison.Ordinal);
    }
}
