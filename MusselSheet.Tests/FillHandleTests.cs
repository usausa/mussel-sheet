namespace MusselSheet;

using MusselSheet.Internal;

public sealed class FillHandleTests : BunitContext
{
    public FillHandleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void NumbersContinueWhenTwoOrMoreAreSelected()
    {
        Assert.Null(FillSeries.Create([10m], false));
        Assert.Equal([11m, 12m], Values<decimal>(FillSeries.Create([10m], true), 1, 2));
        Assert.Equal([30m, 40m, 50m], Values<decimal>(FillSeries.Create([10m, 20m], false), 2, 3, 4));
        Assert.Null(FillSeries.Create([10m, 20m], true));

        Assert.Equal([0m, -10m], Values<decimal>(FillSeries.Create([10m, 20m], false), -1, -2));
        Assert.Equal([0.3m], Values<decimal>(FillSeries.Create([0.1m, 0.2m], false), 2));
        Assert.Equal([3m], Values<decimal>(FillSeries.Create([1, 2.0], false), 2));

        // Uneven spacing follows a least-squares line like Excel; trailing zeros are dropped
        Assert.Equal([5.3333333333333333333333333333m], Values<decimal>(FillSeries.Create([1m, 2m, 4m], false), 3));
        Assert.Equal("7", FillSeries.Create([0m, 1m, 5m], false)!(3)!.ToString());
    }

    [Fact]
    public void DatesContinueByDaysOrMonths()
    {
        var day = new DateOnly(2025, 11, 5);

        Assert.Equal([new DateOnly(2025, 11, 6), new DateOnly(2025, 11, 7)], Values<DateOnly>(FillSeries.Create([day], false), 1, 2));
        Assert.Null(FillSeries.Create([day], true));
        Assert.Equal([new DateOnly(2025, 11, 11)], Values<DateOnly>(FillSeries.Create([day, new DateOnly(2025, 11, 8)], false), 2));

        Assert.Equal([new DateOnly(2025, 6, 1), new DateOnly(2025, 7, 1)], Values<DateOnly>(FillSeries.Create([new DateOnly(2025, 4, 1), new DateOnly(2025, 5, 1)], false), 2, 3));
        Assert.Equal([new DateOnly(2027, 1, 31)], Values<DateOnly>(FillSeries.Create([new DateOnly(2025, 1, 31), new DateOnly(2026, 1, 31)], false), 2));
        Assert.Equal([new DateOnly(2024, 11, 30)], Values<DateOnly>(FillSeries.Create([new DateOnly(2025, 1, 31), new DateOnly(2025, 3, 31)], false), -1));

        Assert.Equal([new DateOnly(2025, 11, 6)], Values<DateOnly>(FillSeries.Create([new DateTime(2025, 11, 5, 0, 0, 0, DateTimeKind.Unspecified)], false), 1));
    }

    [Fact]
    public void TheLastNumberInTextIsIncremented()
    {
        Assert.Equal(["A-002", "A-003"], Values<string>(FillSeries.Create(["A-001"], false), 1, 2));
        Assert.Equal(["A-100"], Values<string>(FillSeries.Create(["A-099"], false), 1));
        Assert.Equal(["第2回", "第3回"], Values<string>(FillSeries.Create(["第1回"], false), 1, 2));
        Assert.Equal(["2月", "12月"], Values<string>(FillSeries.Create(["1月"], false), 1, 11));

        Assert.Equal(["A-005", "A-007"], Values<string>(FillSeries.Create(["A-001", "A-003"], false), 2, 3));
        Assert.Equal(["A2", "B2", "A3"], Values<string>(FillSeries.Create(["A1", "B1"], false), 2, 3, 4));

        Assert.Null(FillSeries.Create(["東京"], false));
        Assert.Null(FillSeries.Create([true], false));
        Assert.Null(FillSeries.Create(["A-001"], true));
        Assert.Null(FillSeries.Create([10m, "A-001"], false));
        Assert.Null(FillSeries.Create([null], false));
    }

    [Fact]
    public async Task DraggingTheHandleFillsEachColumn()
    {
        var sheet = new Sheet(5, 4);
        sheet.Range("A:A").Configure(c => c.Kind = CellKind.Number);
        sheet.Range("B:B").Configure(c => c.Kind = CellKind.Date);
        sheet.SetValues(0, 0, [[10m, new DateOnly(2025, 11, 5), "A-001", "東京"]]);
        var reasons = new List<CellChangeReason>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, e => reasons.Add(e.Reason)));

        var result = await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(0, 0, 0, 3, 0, 0, 2, 3, false));

        Assert.Equal(new PasteResult(0, 0, 2, 3, null), result);
        Assert.Equal([[10m, new DateOnly(2025, 11, 6), "A-002", "東京"], [10m, new DateOnly(2025, 11, 7), "A-003", "東京"]], sheet.Range("A2:D3").GetValues());
        Assert.All(reasons, x => Assert.Equal(CellChangeReason.Fill, x));

        await cut.InvokeAsync(cut.Instance.UndoAsync);
        Assert.All(sheet.Range("A2:D3").GetValues().SelectMany(x => x), Assert.Null);
    }

    [Fact]
    public async Task CtrlSwapsCopyAndSeriesAndFillsUpAndLeft()
    {
        var sheet = new Sheet(6, 4);
        sheet.Range("A:D").Configure(c => c.Kind = CellKind.Number);
        sheet.SetValues(2, 1, [[10m, 20m], [30m, 40m]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(2, 1, 3, 1, 2, 1, 5, 1, true));
        Assert.Equal([[10m], [30m]], sheet.Range("B5:B6").GetValues());

        await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(2, 2, 3, 2, 0, 2, 3, 2, false));
        Assert.Equal([[-20m], [0m]], sheet.Range("C1:C2").GetValues());
        await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(2, 1, 2, 2, 2, 0, 2, 2, false));
        Assert.Equal(0m, sheet.GetValue(2, 0));
    }

    [Fact]
    public async Task FilledCellsFollowTheirOwnRules()
    {
        var sheet = new Sheet(5, 2);
        sheet.Range("A:A").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Maximum = 35;
        });
        sheet.Range("A4").Configure(c => c.ReadOnly = true);
        sheet.Range("B:B").Configure(c => c.Kind = CellKind.Text);
        sheet.SetValues(0, 0, [[10m, null], [20m, null]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(0, 0, 1, 0, 0, 0, 4, 0, false));

        Assert.Equal(30m, sheet.GetValue(2, 0));
        Assert.Null(sheet.GetValue(3, 0));
        Assert.Equal(50m, sheet.GetValue(4, 0));
        Assert.NotNull(sheet.GetError(4, 0));
        Assert.Equal("1 セルは規則に合わないため、エラーの印を付けました", result.Message);

        await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(0, 0, 1, 0, 0, 0, 1, 1, false));
        Assert.Equal("10", sheet.GetValue(0, 1));
    }

    [Fact]
    public async Task FillingCanBeCanceledOrTurnedOff()
    {
        var sheet = new Sheet(3, 1);
        sheet.SetValue(0, 0, "A-001");
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.Filling, e => e.Cancel = e.Target.Bottom > 1));

        await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(0, 0, 0, 0, 0, 0, 2, 0, false));
        Assert.Null(sheet.GetValue(1, 0));
        await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(0, 0, 0, 0, 0, 0, 1, 0, false));
        Assert.Equal("A-002", sheet.GetValue(1, 0));

        Assert.True(cut.Instance.FillHandleEnabled);
        cut.Render(p => p.Add(x => x.FillHandleEnabled, false));
        await cut.InvokeAsync(() => cut.Instance.AutoFillAsync(1, 0, 1, 0, 1, 0, 2, 0, false));
        Assert.Null(sheet.GetValue(2, 0));
    }

    [Fact]
    public async Task DoubleClickFillsDownAsFarAsTheNeighborColumn()
    {
        var sheet = new Sheet(9, 3);
        sheet.Range("B:B").Configure(c => c.Kind = CellKind.Date);
        sheet.SetValues(0, 0, [[1m, new DateOnly(2026, 11, 3)], [2m], [3m], [4m], [5m], [6m, new DateOnly(2027, 1, 1)]]);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await cut.InvokeAsync(() => cut.Instance.AutoFillDownAsync(0, 1, 0, 1));

        Assert.Equal(new PasteResult(0, 1, 4, 1, null), result);
        Assert.Equal(new DateOnly(2026, 11, 7), sheet.GetValue(4, 1));
        Assert.Equal(new DateOnly(2027, 1, 1), sheet.GetValue(5, 1));

        sheet.SetValue(6, 1, new DateOnly(2027, 2, 1));
        sheet.SetValue(7, 2, "y");
        var right = await cut.InvokeAsync(() => cut.Instance.AutoFillDownAsync(6, 1, 6, 1));
        Assert.Equal(new PasteResult(6, 1, 7, 1, null), right);
        Assert.Equal(new DateOnly(2027, 2, 2), sheet.GetValue(7, 1));
        var none = await cut.InvokeAsync(() => cut.Instance.AutoFillDownAsync(7, 1, 7, 1));
        Assert.Equal(new PasteResult(7, 1, 7, 1, null), none);
    }

    private static T[] Values<T>(Func<int, object?>? series, params int[] positions)
    {
        Assert.NotNull(series);
        return [.. positions.Select(x => (T)series(x)!)];
    }
}
