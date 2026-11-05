namespace MusselSheet;

public sealed class StructureMenuTests : BunitContext
{
    public StructureMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task InsertRowsAboveInheritsTheSelectedRow()
    {
        var sheet = FormSheet();
        var cut = RenderSheet(sheet);

        var result = await Change(cut, "insertRowsAbove", new CellRange(1, 1, 1, 1), 1, 1);

        int[] expected = [1, 1, 1, 1, 1, 1];
        Assert.Equal(expected, result);
        Assert.Equal(5, sheet.RowCount);
        Assert.Equal(CellKind.Number, sheet.GetSettings(1, 1).Kind);
        Assert.Equal(CellKind.Label, sheet.GetSettings(0, 1).Kind);
        Assert.Equal(30, sheet.Rows[1].Height);
        Assert.Null(sheet.GetValue(1, 1));
        Assert.Equal(3m, sheet.GetValue(2, 1));
    }

    [Fact]
    public async Task InsertRowsBelowAddsAsManyRowsAsSelectedAndSelectsThem()
    {
        var sheet = FormSheet();
        var cut = RenderSheet(sheet);

        var result = await Change(cut, "insertRowsBelow", new CellRange(1, 0, 2, 1), 1, 0);

        int[] expected = [3, 0, 4, 1, 3, 0];
        Assert.Equal(expected, result);
        Assert.Equal(6, sheet.RowCount);
        Assert.Equal(CellKind.Number, sheet.GetSettings(3, 1).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(4, 1).Kind);
        Assert.Null(sheet.Rows[3].Height);
        Assert.Equal("合計", sheet.GetValue(5, 0));
        Assert.Equal(CellKind.Label, sheet.GetSettings(5, 1).Kind);
    }

    [Fact]
    public async Task InsertedRowsCanBeUndoneAndRedone()
    {
        var sheet = FormSheet();
        var cut = RenderSheet(sheet);
        var original = sheet.Rules.ToArray();
        await Change(cut, "insertRowsBelow", new CellRange(1, 0, 1, 1), 1, 1);
        var inserted = sheet.Rows[2];

        int[] before = [1, 0, 1, 1];
        int[] after = [2, 0, 2, 1];
        Assert.Equal(before, await Undo(cut, false));
        Assert.Equal(4, sheet.RowCount);
        Assert.Equal(original, sheet.Rules);
        Assert.Equal(after, await Undo(cut, true));
        Assert.Same(inserted, sheet.Rows[2]);
        Assert.Equal(CellKind.Number, sheet.GetSettings(2, 1).Kind);
    }

    [Fact]
    public async Task DeletedRowsComeBackWithTheirValuesErrorsHeightsAndSettings()
    {
        var sheet = FormSheet();
        sheet.Range("A2").Configure(c => c.MaxLength = 5);
        sheet.SetError(1, 0, "印");
        var row = sheet.Rows[1];
        var original = sheet.Rules.ToArray();
        var cut = RenderSheet(sheet);

        var result = await Change(cut, "deleteRows", new CellRange(1, 0, 1, 1), 1, 1);

        int[] expected = [1, 0, 1, 1, 1, 1];
        Assert.Equal(expected, result);
        Assert.Equal(3, sheet.RowCount);
        Assert.Equal("みかん", sheet.GetValue(1, 0));
        Assert.Null(sheet.GetSettings(1, 0).MaxLength);

        int[] restored = [1, 0, 1, 1];
        Assert.Equal(restored, await Undo(cut, false));
        Assert.Same(row, sheet.Rows[1]);
        Assert.Equal(3m, sheet.GetValue(1, 1));
        Assert.Equal("印", sheet.GetError(1, 0));
        Assert.Equal(30, sheet.Rows[1].Height);
        Assert.Equal(original, sheet.Rules);

        Assert.Equal(restored, await Undo(cut, true));
        Assert.Equal("みかん", sheet.GetValue(1, 0));
        await Undo(cut, false);
        Assert.Same(row, sheet.Rows[1]);
        Assert.Equal(original, sheet.Rules);
    }

    [Fact]
    public async Task UndoingADeleteKeepsRulesTheAppAddedAfterIt()
    {
        var sheet = FormSheet();
        sheet.Range("A2").Configure(c => c.MaxLength = 5);
        var cut = RenderSheet(sheet);
        await Change(cut, "deleteRows", new CellRange(1, 0, 1, 1), 1, 0);

        await cut.InvokeAsync(() => sheet.Range("3:3").Configure(c => c.ReadOnly = true));

        await Undo(cut, false);

        Assert.Equal(5, sheet.GetSettings(1, 0).MaxLength);
        Assert.False(sheet.GetSettings(1, 0).ReadOnly);
        Assert.True(sheet.GetSettings(3, 0).ReadOnly);
    }

    [Fact]
    public async Task EditsBeforeADeleteCanStillBeUndone()
    {
        var sheet = FormSheet();
        var cut = RenderSheet(sheet);
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(1, 1, "9", true));
        await Change(cut, "deleteRows", new CellRange(1, 0, 1, 1), 1, 1);

        await Undo(cut, false);
        int[] cell = [1, 1, 1, 1];
        Assert.Equal(cell, await Undo(cut, false));
        Assert.Equal(3m, sheet.GetValue(1, 1));
    }

    [Fact]
    public async Task ColumnsCanBeInsertedAndDeletedAndUndone()
    {
        var sheet = new Sheet(2, 3);
        sheet.Range("A:A").Configure(c => c.Kind = CellKind.Label);
        sheet.Range("B:B").Configure(c => c.Kind = CellKind.Number);
        sheet.Columns[1].Width = 120;
        sheet.SetValues(0, 0, [["名前", 1m, "x"], ["値", 2m, "y"]]);
        var cut = RenderSheet(sheet);

        var inserted = await Change(cut, "insertColumnsLeft", new CellRange(0, 1, 1, 1), 0, 1);

        int[] expected = [0, 1, 1, 1, 0, 1];
        Assert.Equal(expected, inserted);
        Assert.Equal(4, sheet.ColumnCount);
        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 1).Kind);
        Assert.Equal(120, sheet.Columns[1].Width);
        Assert.Equal(1m, sheet.GetValue(0, 2));

        var column = sheet.Columns[2];
        await Change(cut, "deleteColumns", new CellRange(0, 2, 1, 2), 0, 2);
        Assert.Equal("x", sheet.GetValue(0, 2));
        await Undo(cut, false);
        Assert.Same(column, sheet.Columns[2]);
        Assert.Equal(2m, sheet.GetValue(1, 2));
        Assert.Equal(CellKind.Number, sheet.GetSettings(1, 2).Kind);

        await Undo(cut, false);
        Assert.Equal(3, sheet.ColumnCount);
        Assert.Equal(1m, sheet.GetValue(0, 1));
    }

    [Fact]
    public async Task OperationsNeedTheirSettingAndTheAppCanCancelThem()
    {
        var sheet = FormSheet();
        var seen = new List<SheetStructureChangingEventArgs>();
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.AllowInsertRows, true)
            .Add(x => x.AllowDeleteRows, true)
            .Add(x => x.RowsInserting, e =>
            {
                seen.Add(e);
                e.Cancel = true;
            })
            .Add(x => x.RowsDeleting, e =>
            {
                seen.Add(e);
                e.Cancel = true;
            }));

        Assert.Null(await Change(cut, "insertRowsAbove", new CellRange(1, 0, 2, 1), 1, 0));
        Assert.Null(await Change(cut, "deleteRows", new CellRange(2, 0, 2, 1), 2, 0));

        Assert.Null(await Change(cut, "insertColumnsLeft", new CellRange(1, 0, 1, 0), 1, 0));

        Assert.Equal(2, seen.Count);
        Assert.IsType<SheetRowsInsertingEventArgs>(seen[0]);
        Assert.Equal(1, seen[0].Index);
        Assert.Equal(2, seen[0].Count);
        Assert.IsType<SheetRowsDeletingEventArgs>(seen[1]);
        Assert.Equal(2, seen[1].Index);
        Assert.Equal(1, seen[1].Count);
        Assert.Equal(4, sheet.RowCount);
        Assert.Equal(2, sheet.ColumnCount);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public async Task EveryRowOrColumnCannotBeDeleted()
    {
        var sheet = FormSheet();
        var cut = RenderSheet(sheet);

        Assert.Null(await Change(cut, "deleteRows", new CellRange(0, 0, 3, 1), 0, 0));
        Assert.Null(await Change(cut, "deleteColumns", new CellRange(0, 0, 3, 1), 0, 0));

        Assert.Equal(4, sheet.RowCount);
        Assert.Equal(2, sheet.ColumnCount);
    }

    private static Sheet FormSheet()
    {
        var sheet = new Sheet(4, 2);
        sheet.Range("1:1").Configure(c => c.Kind = CellKind.Label);
        sheet.Range("B2:B3").Configure(c => c.Kind = CellKind.Number);
        sheet.Range("4:4").Configure(c => c.Kind = CellKind.Label);
        sheet.Rows[1].Height = 30;
        sheet.SetValues(0, 0, [["品名", "数量"], ["りんご", 3m], ["みかん", 4m], ["合計", 7m]]);
        return sheet;
    }

    private static Task<int[]?> Change(IRenderedComponent<MusselSheetView> cut, string action, CellRange selection, int row, int column) =>
        cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync(action, selection.Top, selection.Left, selection.Bottom, selection.Right, row, column));

    private static Task<int[]?> Undo(IRenderedComponent<MusselSheetView> cut, bool redo) =>
        cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(redo));

    private IRenderedComponent<MusselSheetView> RenderSheet(Sheet sheet) =>
        Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.AllowInsertRows, true)
            .Add(x => x.AllowDeleteRows, true)
            .Add(x => x.AllowInsertColumns, true)
            .Add(x => x.AllowDeleteColumns, true));
}
