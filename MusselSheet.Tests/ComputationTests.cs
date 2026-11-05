namespace MusselSheet;

using Bunit;

public sealed class ComputationTests : BunitContext
{
    public ComputationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ComputedCellFollowsTheObservedCells()
    {
        var sheet = new Sheet(3, 1);
        sheet.SetValues(0, 0, [[1m], [2m]]);
        using var sum = sheet.Compute("A3", ["A1", "A2"], x => x.Get<decimal>("A1") + x.Get<decimal>("A2"));

        Assert.Equal(3m, sheet.GetValue(2, 0));
        sheet.SetValue(0, 0, 10m);
        Assert.Equal(12m, sheet.GetValue(2, 0));
        Assert.True(sheet.GetSettings(2, 0).ReadOnly);

        sheet.Range("A3").Configure(c => c.ReadOnly = false);
        Assert.False(sheet.GetSettings(2, 0).ReadOnly);
    }

    [Fact]
    public void ContextReadsOnlyTheObservedCells()
    {
        var sheet = new Sheet(2, 2);
        sheet.SetValues(0, 0, [["x", 5m]]);
        string? seen = null;
        using var computation = sheet.Compute("A2", ["A1", "B1"], x =>
        {
            seen = $"{x.Get<decimal>("A1")}/{x.Get<decimal?>("A1")}/{x.Get<int>("b1")}/{x.Address}";
            return null;
        });

        Assert.Equal("0//5/A2", seen);
        Assert.Throws<ArgumentException>(() => sheet.Compute("B2", ["A1"], x => x.Get<decimal>("B1")));
        Assert.Throws<ArgumentException>(() => sheet.Compute("B2", ["Z99"], _ => null));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.ComputeRows("A1:B2", ["A"], _ => null));
    }

    [Fact]
    public void RowsAndTotalsAreComputedOnceForABatch()
    {
        var sheet = DetailSheet();
        var calls = 0;
        using var amounts = sheet.ComputeRows("D2:D4", ["B", "C"], x =>
        {
            calls++;
            return x.Get<decimal?>("B") * x.Get<decimal?>("C");
        });
        using var total = sheet.Compute("D5", ["D2:D4"], x => x.Values("D2:D4").OfType<decimal>().Sum());

        Assert.Equal(1200m, sheet.GetValue(1, 3));
        Assert.Null(sheet.GetValue(2, 3));
        Assert.Equal(2800m, sheet.GetValue(4, 3));

        var before = calls;
        using (sheet.BeginUpdate())
        {
            sheet.SetValues(2, 1, [[5m, 100m]]);
            sheet.SetValue(2, 1, 6m);
        }

        Assert.Equal(before + 1, calls);
        Assert.Equal(600m, sheet.GetValue(2, 3));
        Assert.Equal(3400m, sheet.GetValue(4, 3));
    }

    [Fact]
    public void RowComputationsCanObserveOtherCells()
    {
        var sheet = DetailSheet();
        sheet.SetValue(0, 3, 2m);

        using var amounts = sheet.ComputeRows("D2:D4", ["B", "C", "D1"], x => x.Get<decimal?>("B") * x.Get<decimal?>("C") * x.Get<decimal>("D1"));
        Assert.Equal(2400m, sheet.GetValue(1, 3));
        sheet.SetValue(0, 3, 3m);
        Assert.Equal(3600m, sheet.GetValue(1, 3));
        Assert.Equal(4800m, sheet.GetValue(3, 3));
    }

    [Fact]
    public void RangesGrowWithTheDetailAndNamesFollowTheCells()
    {
        var sheet = DetailSheet();
        using var amounts = sheet.ComputeRows("D2:D4", ["B", "C"], x => x.Get<decimal?>("B") * x.Get<decimal?>("C"));
        using var total = sheet.Compute("D5", ["D2:D4"], x => x.Values("D2:D4").OfType<decimal>().Sum());

        sheet.InsertRows(4, 1);
        sheet.SetValues(4, 1, [[2m, 50m]]);
        Assert.Equal(100m, sheet.GetValue(4, 3));
        Assert.True(sheet.GetSettings(4, 3).ReadOnly);
        Assert.Equal(2900m, sheet.GetValue(5, 3));

        sheet.InsertRows(0, 1);
        sheet.SetValue(2, 1, 3m);
        Assert.Equal(360m, sheet.GetValue(2, 3));
        Assert.Equal(2060m, sheet.GetValue(6, 3));

        sheet.DeleteRows(2, 1);
        Assert.Equal(1700m, sheet.GetValue(5, 3));
    }

    [Fact]
    public async Task UndoRecomputesFromTheRestoredValues()
    {
        var sheet = DetailSheet();
        using var amounts = sheet.ComputeRows("D2:D4", ["B", "C"], x => x.Get<decimal?>("B") * x.Get<decimal?>("C"));
        using var total = sheet.Compute("D5", ["D2:D4"], x => x.Values("D2:D4").OfType<decimal>().Sum());
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.AllowDeleteRows, true));

        await cut.InvokeAsync(() => cut.Instance.CommitAsync(2, 1, "3", false));
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(2, 2, "70", false));
        Assert.Equal(210m, sheet.GetValue(2, 3));
        Assert.Equal(3010m, sheet.GetValue(4, 3));

        await cut.InvokeAsync(cut.Instance.UndoAsync);
        Assert.Null(sheet.GetValue(2, 3));
        Assert.Equal(2800m, sheet.GetValue(4, 3));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, 1, 3, 1, 0));
        Assert.Equal(1600m, sheet.GetValue(3, 3));
        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false));
        Assert.Equal(2800m, sheet.GetValue(4, 3));
    }

    [Fact]
    public void LoopsStopAndDisposeStopsComputing()
    {
        var sheet = new Sheet(2, 2);
        using var first = sheet.Compute("A1", ["B1"], x => x.Get<decimal>("B1") + 1);
        var second = sheet.Compute("B1", ["A1"], x => x.Get<decimal>("A1") + 1);

        Assert.Equal(3m, sheet.GetValue(0, 0));
        Assert.Equal(2m, sheet.GetValue(0, 1));

        second.Dispose();
        decimal[] rate = [1m];
        using var price = sheet.Compute("A2", ["B2"], x => x.Get<decimal>("B2") * rate[0]);
        sheet.SetValue(1, 1, 100m);
        rate[0] = 1.1m;
        Assert.Equal(100m, sheet.GetValue(1, 0));
        price.Refresh();
        Assert.Equal(110.0m, sheet.GetValue(1, 0));
    }

    private static Sheet DetailSheet()
    {
        var sheet = new Sheet(5, 4);
        sheet.Range("B2:C4").Configure(c => c.Kind = CellKind.Number);
        sheet.SetValues(0, 0, [
            ["品名", "数量", "単価", "金額"],
            ["ノート", 10m, 120m],
            [],
            ["ペン", 20m, 80m]
        ]);
        return sheet;
    }
}
