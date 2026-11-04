namespace MusselSheet;

using System.Globalization;

using Bunit;

using MusselSheet.Internal;

public sealed class JapaneseEraTests : BunitContext
{
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");

    private static readonly InputContext Context = new(Japanese, new DateOnly(2026, 11, 2), SheetTexts.Japanese, "yyyy/MM/dd", ReadingRules.From(Japanese, true));

    public JapaneseEraTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData(2025, 11, 5, "ggge年M月d日", "令和7年11月5日")]
    [InlineData(2019, 5, 1, "ggge年M月d日", "令和元年5月1日")]
    [InlineData(2019, 4, 30, "ggge年M月d日", "平成31年4月30日")]
    [InlineData(1989, 1, 7, "ggge年M月d日", "昭和64年1月7日")]
    [InlineData(1989, 1, 8, "ggge年M月d日", "平成元年1月8日")]
    [InlineData(2025, 11, 5, "ge.M.d", "R7.11.5")]
    [InlineData(2025, 11, 5, "gge年", "令7年")]
    [InlineData(2025, 1, 5, "ggg ee/MM/dd", "令和 07/01/05")]
    [InlineData(2019, 5, 1, "gee年", "R01年")]
    [InlineData(2025, 11, 5, "ggge年M月d日(ddd)", "令和7年11月5日(水)")]
    [InlineData(2025, 11, 5, "ggge'年度'", "令和7年度")]
    [InlineData(2025, 11, 5, "ggge年(yyyy年)", "令和7年(2025年)")]
    public void EraCodesInTheFormatShowTheJapaneseEra(int year, int month, int day, string format, string expected)
    {
        Assert.Equal(expected, CellText.FormatDate(new DateOnly(year, month, day), format, Japanese, SheetJapaneseEra.BuiltIn));
    }

    [Fact]
    public void DatesBeforeTheFirstEraUseTheShortDateFormat()
    {
        Assert.Equal("1800/01/01", CellText.FormatDate(new DateOnly(1800, 1, 1), "ggge年M月d日", Japanese, SheetJapaneseEra.BuiltIn));
        Assert.Equal("2025/11/05", CellText.FormatDate(new DateOnly(2025, 11, 5), "yyyy/MM/dd", Japanese, SheetJapaneseEra.BuiltIn));
    }

    // Years past an era's end are accepted and counted on from its start (H32 = 2020)
    [Theory]
    [InlineData("令和7年11月5日", 2025, 11, 5)]
    [InlineData("令和元年5月1日", 2019, 5, 1)]
    [InlineData("令和7/11/5", 2025, 11, 5)]
    [InlineData("令和7.11.5", 2025, 11, 5)]
    [InlineData("R7.11.5", 2025, 11, 5)]
    [InlineData("r7/11/5", 2025, 11, 5)]
    [InlineData("H31.4.30", 2019, 4, 30)]
    [InlineData("H31.5.1", 2019, 5, 1)]
    [InlineData("H32.1.1", 2020, 1, 1)]
    [InlineData("平成32年1月1日", 2020, 1, 1)]
    [InlineData("S64.1.7", 1989, 1, 7)]
    [InlineData("M45.7.29", 1912, 7, 29)]
    public void JapaneseEraDatesAreRead(string text, int year, int month, int day)
    {
        Assert.True(CellInput.TryConvert(text, Date, Context, out var value, out _));
        Assert.Equal(new DateOnly(year, month, day), value);
    }

    [Theory]
    [InlineData("令和1年4月30日")]
    [InlineData("R0.1.1")]
    [InlineData("X7.1.1")]
    [InlineData("R071105")]
    [InlineData("令和7年11月5")]
    [InlineData("R7.13.1")]
    public void OtherEraTextIsRejected(string text)
    {
        Assert.False(CellInput.TryConvert(text, Date, Context, out _, out _));
    }

    [Fact]
    public void FullwidthEraTextAndBoundsUseTheEra()
    {
        Assert.True(CellInput.TryConvert(CellInput.Normalize("Ｒ７．１１．５", CellKind.Date, Context.Reading), Date, Context, out var value, out _));
        Assert.Equal(new DateOnly(2025, 11, 5), value);

        var english = Context with { Reading = ReadingRules.From(Japanese, false) };
        Assert.False(CellInput.TryConvert("R7.11.5", Date, english, out _, out _));

        var settings = new ResolvedCellSettings { Kind = CellKind.Date, Format = "ggge年M月d日", Minimum = new DateOnly(2026, 1, 1) };
        Assert.Equal("令和8年1月1日 以上で入力してください", CellInput.Validate(new DateOnly(2025, 12, 31), "R7.12.31", settings, Context));
    }

    [Fact]
    public async Task RegisteredErasAreUsedForInputAndDisplay()
    {
        Services.AddMusselSheet(o => o.Eras.Add(new SheetJapaneseEra("新元", "新", "N", new DateOnly(2040, 1, 1))));
        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(c =>
        {
            c.Kind = CellKind.Date;
            c.Format = "ggge年M月d日";
        });
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "N2.3.4", false));

        Assert.Equal(new DateOnly(2041, 3, 4), sheet.GetValue(0, 0));
        Assert.Equal("新元2年3月4日", cut.Find(".mussel-sheet-row[data-r='0'] .mussel-sheet-cell[data-c='0']").TextContent);
    }

    private static ResolvedCellSettings Date => new() { Kind = CellKind.Date };
}
