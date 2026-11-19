namespace MusselSheet;

using System.Collections.ObjectModel;

using MusselSheet.Internal;

public sealed class ObjectBindingEdgeTests : BunitContext
{
    public enum Status
    {
        Open,

        Closed
    }

    public ObjectBindingEdgeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ASetterThatThrowsMarksTheCell()
    {
        var target = new Target();
        var sheet = new Sheet(1, 1);
        using var binding = sheet.Bind(target).Cell("A1", static x => x.Guarded);

        sheet.SetValue(0, 0, "bad");

        Assert.Equal("Not allowed", sheet.GetError(0, 0));
        Assert.Equal("ok", target.Guarded);
    }

    [Fact]
    public void MergingTellsTheBindingsThatTheCoveredValuesAreGone()
    {
        var line = new Line { Name = "b" };
        var sheet = new Sheet(1, 2);
        using var binding = sheet.Bind(line).Cell("B1", static x => x.Name);

        sheet.Range("A1:B1").Merge();

        Assert.Null(line.Name);
    }

    [Fact]
    public void AMergeLeavesTheCoveredCellOfAValuePropertyEmpty()
    {
        var target = new Target { Amount = 5m };
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.All, static c => c.Kind = CellKind.Number);
        using var binding = sheet.Bind(target).Cell("B1", static x => x.Amount);

        sheet.Range("A1:B1").Merge();

        Assert.Equal((0m, null), (target.Amount, sheet.GetValue(0, 1)));
    }

    [Fact]
    public void AConvertedExpressionStillFindsTheProperty()
    {
        var target = new Target { Count = 3 };
        var sheet = new Sheet(1, 1);
        using var binding = sheet.Bind(target).Cell("A1", static x => (object)x.Count);

        Assert.Equal(3, sheet.GetValue(0, 0));
        sheet.SetValue(0, 0, 5m);
        Assert.Equal(5, target.Count);
    }

    [Fact]
    public void AnExpressionMustBeAProperty()
    {
        var sheet = new Sheet(1, 1);

        Assert.Throws<ArgumentException>(() => sheet.Bind(new Target()).Cell("A1", static x => x.Count + 1));
    }

    [Fact]
    public void EnumsAreReadFromText()
    {
        var target = new Target();
        var sheet = new Sheet(1, 1);
        using var binding = sheet.Bind(target).Cell("A1", static x => x.State);

        sheet.SetValue(0, 0, "closed");
        Assert.Equal(Status.Closed, target.State);

        sheet.SetValue(0, 0, "unknown");
        Assert.Equal(Status.Closed, target.State);
    }

    [Theory]
    [InlineData(1.5d)]
    [InlineData(1.5f)]
    public void FractionsDoNotGoToWholeNumbers(object value)
    {
        var target = new Target { Count = 3 };
        var sheet = new Sheet(1, 1);
        using var binding = sheet.Bind(target).Cell("A1", static x => x.Count);

        sheet.SetValue(0, 0, value);

        Assert.Equal(3, target.Count);
    }

    [Fact]
    public async Task TheMessageFollowsTheTypeOfTheProperty()
    {
        var target = new Target();
        var sheet = new Sheet(1, 6);
        using var binding = sheet.Bind(target)
            .Cell("A1", static x => x.Count)
            .Cell("B1", static x => x.Amount)
            .Cell("C1", static x => x.Date)
            .Cell("D1", static x => x.Time)
            .Cell("E1", static x => x.Done)
            .Cell("F1", static x => x.State);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var texts = JapaneseTexts.Default;

        Assert.Equal(texts.WholeNumber, await Commit(0));
        Assert.Equal(texts.Number, await Commit(1));
        Assert.Equal("日付を yyyy/MM/dd の形で入力してください", await Commit(2));
        Assert.Equal(texts.Time, await Commit(3));
        Assert.Equal(texts.Check, await Commit(4));
        Assert.Equal(texts.Pattern, await Commit(5));

        async Task<string?> Commit(int column) =>
            (await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, column, "abc", false)).ConfigureAwait(true)).Error;
    }

    [Fact]
    public void ChangingOneCellOfManyRowsDoesNotCopyTheRows()
    {
        const int count = 20_000;
        var target = new ListTarget();
        for (var i = 0; i < count; i++)
        {
            target.Lines.Add(new Line { Name = "x" });
        }

        var sheet = new Sheet(count, 2);
        using var binding = sheet.Bind(target).Rows($"A1:B{count}", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        sheet.SetValue(0, 0, "warm");

        var before = GC.GetAllocatedBytesForCurrentThread();
        sheet.SetValue(count - 1, 0, "y");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("y", target.Lines[^1].Name);
        Assert.True(allocated < 32 * 1024, $"{allocated} bytes");
    }

    [Fact]
    public void AColumnAddedAfterBindingIsShown()
    {
        var target = new ListTarget { Lines = { new Line { Name = "a", Note = "b" } } };
        var sheet = new Sheet(2, 2);
        SheetRowsBinding<Line>? rows = null;
        using var binding = sheet.Bind(target).Rows("A1:B2", static x => x.Lines, x =>
        {
            rows = x;
            x.Column("A", static y => y.Name);
        });
        Assert.Null(sheet.GetValue(0, 1));

        rows!.Column("B", static y => y.Note);

        Assert.Equal("b", sheet.GetValue(0, 1));
    }

    [Fact]
    public async Task AColumnAddedAfterBindingClearsTheHistory()
    {
        var target = new ListTarget { Lines = { new Line { Name = "a", Note = "b" } } };
        var sheet = new Sheet(2, 2);
        SheetRowsBinding<Line>? rows = null;
        using var binding = sheet.Bind(target).Rows("A1:B2", static x => x.Lines, x =>
        {
            rows = x;
            x.Column("A", static y => y.Name);
        });
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);
        await cut.InvokeAsync(() => bridge.CommitAsync(0, 1, "typed", true, null));
        Assert.True(cut.Instance.CanUndo);

        await cut.InvokeAsync(() => rows!.Column("B", static y => y.Note));

        Assert.False(cut.Instance.CanUndo);
    }

    [Fact]
    public void ARowInsertedBetweenTwoListsJoinsOnlyTheUpperOne()
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(4, 1);
        using var upper = sheet.Bind(first).Rows("A1:A2", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var lower = sheet.Bind(second).Rows("A3:A4", static x => x.Lines, static x => x.Column("A", static y => y.Name));

        sheet.InsertRows(2, 1);
        sheet.SetValue(2, 0, "x");

        Assert.Equal(["a", "b", "x"], first.Lines.Select(static x => x.Name));
        Assert.Equal(["c", "d"], second.Lines.Select(static x => x.Name));
    }

    [Fact]
    public void ARowInsertedWhereTheUpperListIsEmptyJoinsOnlyThatList()
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" } } };
        var sheet = new Sheet(2, 1);
        using var upper = sheet.Bind(first).Rows("A1:A1", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var lower = sheet.Bind(second).Rows("A2:A2", static x => x.Lines, static x => x.Column("A", static y => y.Name));

        sheet.DeleteRows(0, 1);
        sheet.InsertRows(0, 1);
        sheet.SetValue(0, 0, "x");

        Assert.Equal(["x"], first.Lines.Select(static x => x.Name));
        Assert.Equal(["c"], second.Lines.Select(static x => x.Name));
    }

    [Fact]
    public void ARowInsertedWhereTheLowerListIsEmptyJoinsOnlyTheUpperOne()
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" } } };
        var sheet = new Sheet(2, 1);
        using var upper = sheet.Bind(first).Rows("A1:A1", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var lower = sheet.Bind(second).Rows("A2:A2", static x => x.Lines, static x => x.Column("A", static y => y.Name));

        sheet.DeleteRows(1, 1);
        sheet.InsertRows(1, 1);
        sheet.SetValue(1, 0, "x");

        Assert.Equal(["a", "x"], first.Lines.Select(static x => x.Name));
        Assert.Empty(second.Lines);
    }

    [Fact]
    public void RowsAListAddsForItsElementsStayOutOfTheListAbove()
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" } } };
        var sheet = new Sheet(2, 1);
        using var upper = sheet.Bind(first).Rows("A1:A1", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var lower = sheet.Bind(second).Rows("A2:A2", static x => x.Lines, static x => x.Column("A", static y => y.Name));

        second.Lines.Insert(0, new Line { Name = "z" });

        Assert.Equal(["a"], first.Lines.Select(static x => x.Name));
        Assert.Equal(["z", "c"], second.Lines.Select(static x => x.Name));
        Assert.Equal(["a", "z", "c"], Enumerable.Range(0, 3).Select(r => sheet.GetValue(r, 0) as string));
    }

    [Fact]
    public void RowsPutBackReturnOnlyToTheirOwnLists()
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(5, 1);
        using var upper = sheet.Bind(first).Rows("A1:A2", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var lower = sheet.Bind(second).Rows("A3:A4", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        var rules = sheet.Rules.ToArray();
        var head = sheet.Rows[2];
        var total = sheet.Rows[4];

        sheet.DeleteRows(2, 1);
        sheet.PutRows(2, [head], rules, []);
        sheet.DeleteRows(4, 1);
        sheet.PutRows(4, [total], rules, []);
        sheet.SetValue(4, 0, "total");

        Assert.Equal(["a", "b"], first.Lines.Select(static x => x.Name));
        Assert.Equal(["c", "d"], second.Lines.Select(static x => x.Name));
    }

    [Fact]
    public void ARowPutBackIsComputedAgainWithTheValueItShowsAgain()
    {
        var target = new Target { Amount = 10m };
        var sheet = new Sheet(3, 3);
        sheet.Configure(CellRange.All, static c => c.Kind = CellKind.Number);
        using var binding = sheet.Bind(target).Cell("A2", static x => x.Amount);
        sheet.ComputeRows("C1:C3", ["A", "B"], static x => x.Get<decimal?>("A") * x.Get<decimal?>("B"));
        sheet.SetValue(1, 1, 2m);
        var row = sheet.Rows[1];
        var rules = sheet.Rules.ToArray();

        sheet.DeleteRows(1, 1);
        target.Amount = 50m;
        sheet.PutRows(1, [row], rules, []);

        Assert.Equal(100m, sheet.GetValue(1, 2));
    }

    [Fact]
    public void OnlyOneOfTwoEmptyListsAtTheSamePlaceTakesAnInsertedRow()
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(6, 1);
        using var upper = sheet.Bind(first).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var lower = sheet.Bind(second).Rows("A4:A5", static x => x.Lines, static x => x.Column("A", static y => y.Name));

        sheet.DeleteRows(1, 4);
        sheet.InsertRows(1, 1);
        sheet.SetValue(1, 0, "x");

        Assert.Equal(1, first.Lines.Count + second.Lines.Count);
    }

    [Fact]
    public void AnEmptyListMovesWithTheRowsItsSiblingAdds()
    {
        var target = new TwoLists { Lines = { new Line { Name = "a" }, new Line { Name = "b" } }, Others = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(8, 1);
        using var binding = sheet.Bind(target)
            .Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name))
            .Rows("A6:A7", static x => x.Others, static x => x.Column("A", static y => y.Name));

        sheet.DeleteRows(5, 2);
        target.Lines.Add(new Line { Name = "e" });
        target.Others.Add(new Line { Name = "o" });

        Assert.Equal("o", sheet.GetValue(6, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnEmptyListStaysAboveTheRowsTheListBelowAddsAtItsTop(bool sameBind)
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var target = new TwoLists { Lines = { new Line { Name = "a" }, new Line { Name = "b" } }, Others = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(6, 1);
        var upper = sameBind ? target.Lines : first.Lines;
        var lower = sameBind ? target.Others : second.Lines;
        using var together = sameBind ? sheet.Bind(target).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name)).Rows("A4:A5", static x => x.Others, static x => x.Column("A", static y => y.Name)) : null;
        using var above = sameBind ? null : sheet.Bind(first).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var below = sameBind ? null : sheet.Bind(second).Rows("A4:A5", static x => x.Lines, static x => x.Column("A", static y => y.Name));

        sheet.DeleteRows(1, 2);
        lower.Insert(0, new Line { Name = "x" });
        upper.Add(new Line { Name = "u" });

        Assert.Equal(["u", "x", "c", "d"], Enumerable.Range(1, 4).Select(r => sheet.GetValue(r, 0)));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task AnEmptyListStaysAboveTheRowsThatUndoBringsBackToTheListBelow(bool sameBind, int count)
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var target = new TwoLists { Lines = { new Line { Name = "a" }, new Line { Name = "b" } }, Others = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(6, 1);
        var upper = sameBind ? target.Lines : first.Lines;
        using var together = sameBind ? sheet.Bind(target).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name)).Rows("A4:A5", static x => x.Others, static x => x.Column("A", static y => y.Name)) : null;
        // Bound first, the lower list takes its rows back before the upper list sees them
        using var below = sameBind ? null : sheet.Bind(second).Rows("A4:A5", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var above = sameBind ? null : sheet.Bind(first).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.AllowDeleteRows, true));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, 2, 0, 1, 0));
        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, count, 0, 1, 0));
        await cut.InvokeAsync(cut.Instance.UndoAsync);
        upper.Add(new Line { Name = "u" });

        Assert.Equal(["u", "c", "d"], Enumerable.Range(1, 3).Select(r => sheet.GetValue(r, 0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEmptyListMovesOnlyByTheRowsThatWereAboveItWhenUndoBringsThemBack(bool sameBind)
    {
        var first = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var second = new ListTarget { Lines = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var target = new TwoLists { Lines = { new Line { Name = "a" }, new Line { Name = "b" } }, Others = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(6, 1);
        sheet.SetValue(0, 0, "H");
        var upper = sameBind ? target.Lines : first.Lines;
        using var together = sameBind ? sheet.Bind(target).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name)).Rows("A4:A5", static x => x.Others, static x => x.Column("A", static y => y.Name)) : null;
        using var below = sameBind ? null : sheet.Bind(second).Rows("A4:A5", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        using var above = sameBind ? null : sheet.Bind(first).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.AllowDeleteRows, true));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, 2, 0, 1, 0));
        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 0, 0, 1, 0, 0, 0));
        await cut.InvokeAsync(cut.Instance.UndoAsync);
        upper.Add(new Line { Name = "u" });

        Assert.Equal(["H", "u", "c", "d"], Enumerable.Range(0, 4).Select(r => sheet.GetValue(r, 0)));
    }

    [Fact]
    public async Task AnEmptyListStaysAboveARowWithoutAListThatUndoBringsBackBelowIt()
    {
        var target = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var sheet = new Sheet(5, 1);
        sheet.SetValue(0, 0, "H");
        sheet.SetValue(3, 0, "T");
        using var binding = sheet.Bind(target).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.AllowDeleteRows, true));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, 2, 0, 1, 0));
        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, 1, 0, 1, 0));
        await cut.InvokeAsync(cut.Instance.UndoAsync);
        target.Lines.Add(new Line { Name = "u" });

        Assert.Equal(["H", "u", "T"], Enumerable.Range(0, 3).Select(r => sheet.GetValue(r, 0)));
    }

    [Fact]
    public async Task AnEmptyListMovesBelowARowThatWasAboveItsRowsWhenUndoBringsItBack()
    {
        var target = new ListTarget { Lines = { new Line { Name = "a" }, new Line { Name = "b" } } };
        var sheet = new Sheet(5, 1);
        sheet.SetValue(0, 0, "H");
        using var binding = sheet.Bind(target).Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.AllowDeleteRows, true));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 0, 0, 0, 0, 0, 0));
        await cut.InvokeAsync(() => sheet.DeleteRows(0, 2));
        await cut.InvokeAsync(cut.Instance.UndoAsync);
        target.Lines.Add(new Line { Name = "u" });

        Assert.Equal(["H", "u"], Enumerable.Range(0, 2).Select(r => sheet.GetValue(r, 0)));
    }

    [Fact]
    public async Task TheUserCannotDeleteTheRowOfAnElementOfAReadOnlyList()
    {
        var inner = new ObservableCollection<Line> { new() { Name = "a" }, new() { Name = "b" }, new() { Name = "c" } };
        var target = new ReadOnlyListTarget(inner);
        var sheet = new Sheet(5, 1);
        using var binding = sheet.Bind(target).Rows("A1:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name));
        var deleting = 0;
        var cut = Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.AllowDeleteRows, true)
            .Add(x => x.AllowInsertRows, true)
            .Add(x => x.RowsDeleting, _ => deleting++));

        var refused = await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 1, 0, 1, 0, 1, 0));
        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("insertRowsBelow", 2, 0, 2, 0, 2, 0));
        var empty = await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 3, 0, 3, 0, 3, 0));
        inner.RemoveAt(1);

        Assert.Null(refused);
        Assert.NotNull(empty);
        Assert.Equal(1, deleting);
        Assert.Equal(["a", "c"], target.Lines.Select(static x => x.Name));
        Assert.Equal(["a", "c"], Enumerable.Range(0, 2).Select(r => sheet.GetValue(r, 0)));
    }

    [Fact]
    public void OfTwoEmptyListsAtOnePlaceTheUpperStaysAboveTheRowsOfTheLower()
    {
        var target = new TwoLists { Lines = { new Line { Name = "a" }, new Line { Name = "b" } }, Others = { new Line { Name = "c" }, new Line { Name = "d" } } };
        var sheet = new Sheet(6, 1);
        using var binding = sheet.Bind(target)
            .Rows("A2:A3", static x => x.Lines, static x => x.Column("A", static y => y.Name))
            .Rows("A4:A5", static x => x.Others, static x => x.Column("A", static y => y.Name));

        sheet.DeleteRows(1, 4);
        target.Others.Add(new Line { Name = "x" });
        target.Lines.Add(new Line { Name = "u" });

        Assert.Equal(["u", "x"], Enumerable.Range(1, 2).Select(r => sheet.GetValue(r, 0)));
    }

    [Fact]
    public void AColumnMustBeInsideTheRows()
    {
        var sheet = new Sheet(2, 3);

        Assert.Throws<ArgumentException>(() => sheet.Bind(new ListTarget()).Rows("A1:B2", static x => x.Lines, static x => x.Column("?", static y => y.Name)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Bind(new ListTarget()).Rows("A1:B2", static x => x.Lines, static x => x.Column("C", static y => y.Name)));
    }

    [Fact]
    public async Task TheEditTextOfABoundDoubleCommitsAsItIs()
    {
        var target = new DoubleTarget { Ratio = 0.00005 };
        var sheet = new Sheet(1, 1);
        sheet[0, 0].Configure(static c => c.Kind = CellKind.Number);
        using var binding = sheet.Bind(target).Cell("A1", static x => x.Ratio);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var result = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, sheet.GetEditText(0, 0), false));

        Assert.True(result.Ok);
        Assert.Equal(0.00005, target.Ratio);
    }

    [Fact]
    public async Task ADateTypedForADateTimeOffsetIsNotEditableAndAnEmptyCellWritesNull()
    {
        var target = new OffsetTarget { When = new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.FromHours(9)) };
        var sheet = new Sheet(1, 1);
        sheet[0, 0].Configure(static c => c.Kind = CellKind.Date);
        using var binding = sheet.Bind(target).Cell("A1", static x => x.When);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var typed = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, sheet.GetEditText(0, 0), false));
        var emptied = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, string.Empty, false));

        Assert.Equal(JapaneseTexts.Default.NotEditable, typed.Error);
        Assert.True(emptied.Ok);
        Assert.Null(target.When);
    }

    [Fact]
    public void ASetterThatDeletesTheRowOfItsCellWritesNothingIntoTheNextRow()
    {
        var target = new Hooked();
        var sheet = new Sheet(3, 1);
        using var binding = sheet.Bind(target).Cell("A2", static x => x.Value);
        target.AfterSet = () => sheet.DeleteRows(1, 1);
        sheet.SetValue(2, 0, 9m);

        sheet.SetValue(1, 0, 5m);

        Assert.Equal(9m, sheet.GetValue(1, 0));
    }

    [Fact]
    public void ASetterThatRemovesItsElementWritesNothingIntoTheNextRow()
    {
        var target = Removables(5m, 7m, 9m);
        var sheet = new Sheet(4, 1);
        using var binding = sheet.Bind(target).Rows("A1:A1", static x => x.Lines, static x => x.Column("A", static y => y.Quantity));

        sheet.SetValue(1, 0, -1m);

        Assert.Equal([5m, 9m], target.Lines.Select(static x => x.Quantity));
        Assert.Equal(9m, sheet.GetValue(1, 0));
    }

    [Fact]
    public void ACellOfAnotherBindTakesItsValueAfterASetterMovedItsRow()
    {
        var lists = new HookedLists { Upper = { new Hooked(), new Hooked() } };
        lists.Upper[0].AfterSet = () => lists.Upper.Add(new Hooked());
        var single = new Hooked();
        var sheet = new Sheet(3, 1);
        using var rows = sheet.Bind(lists).Rows("A1:A2", static x => x.Upper, static x => x.Column("A", static y => y.Value));
        using var cell = sheet.Bind(single).Cell("A3", static x => x.Value);

        sheet.SetValues(0, 0, [[200m], [1m], [5m]]);

        Assert.Equal(5m, sheet.GetValue(3, 0));
        Assert.Equal(5m, single.Value);
    }

    [Fact]
    public void ARowAddedAfterEveryDetailRowIsGoneTakesNothingFromTheRowAbove()
    {
        var target = new HookedLists { Upper = { new Hooked(), new Hooked() } };
        var sheet = new Sheet(3, 1);
        sheet["A1"].Configure(static c => c.Kind = CellKind.Label);
        using var binding = sheet.Bind(target).Rows("A2:A3", static x => x.Upper, static x => x.Column("A", static y => y.Value));
        sheet.DeleteRows(1, 2);

        target.Upper.Add(new Hooked());

        Assert.Equal(2, sheet.RowCount);
        Assert.NotEqual(CellKind.Label, sheet.GetSettings(1, 0).Kind);
    }

    [Fact]
    public void ASetterThatAddsARowAboveLeavesTheComputationOnTheMovedRow()
    {
        var target = new HookedLists { Upper = { new Hooked() }, Lower = { new Hooked() } };
        target.Lower[0].AfterSet = () => target.Upper.Add(new Hooked());
        var sheet = new Sheet(3, 2);
        using var binding = sheet.Bind(target)
            .Rows("A1:A1", static x => x.Upper, static x => x.Column("A", static y => y.Value))
            .Rows("A2:A2", static x => x.Lower, static x => x.Column("A", static y => y.Value));
        sheet.ComputeRows("B1:B3", ["A"], static c => c.Get<decimal>("A") * 2);

        sheet.SetValue(1, 0, 5m);

        Assert.Equal(5m, sheet.GetValue(2, 0));
        Assert.Equal(10m, sheet.GetValue(2, 1));
    }

    [Fact]
    public async Task ACommitWhoseSetterAddsARowAboveGivesTheCellWhereItEnds()
    {
        var target = new HookedLists { Upper = { new Hooked() }, Lower = { new Hooked() } };
        target.Lower[0].AfterSet = () => target.Upper.Add(new Hooked());
        var sheet = new Sheet(2, 1);
        using var binding = sheet.Bind(target)
            .Rows("A1:A1", static x => x.Upper, static x => x.Column("A", static y => y.Value))
            .Rows("A2:A2", static x => x.Lower, static x => x.Column("A", static y => y.Value));
        var addresses = new List<CellAddress>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CellCommitted, e => addresses.Add(e.Address)));
        var bridge = new SheetScriptBridge(cut.Instance);

        await cut.InvokeAsync(() => bridge.CommitAsync(1, 0, "5", false, null));
        var undone = await cut.InvokeAsync(() => bridge.UndoFromScriptAsync(false));

        Assert.Equal([new CellAddress(2, 0), new CellAddress(3, 0)], addresses);
        Assert.Equal([3, 0, 3, 0], undone!);
    }

    [Fact]
    public async Task ACommitWhoseSetterRemovesTheRowLeavesTheNextRowAloneOnUndo()
    {
        var target = Removables(5m, 7m, 9m);
        var sheet = new Sheet(4, 1);
        using var binding = sheet.Bind(target).Rows("A1:A1", static x => x.Lines, static x => x.Column("A", static y => y.Quantity));
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);

        await cut.InvokeAsync(() => bridge.CommitAsync(1, 0, "-1", false, null));
        await cut.InvokeAsync(cut.Instance.UndoAsync);

        Assert.Equal([5m, 9m], target.Lines.Select(static x => x.Quantity));
        Assert.Equal(9m, sheet.GetValue(1, 0));
    }

    [Fact]
    public void ValuesSetOverRowsWhoseSetterRemovesAnElementReachTheRowsBelow()
    {
        var target = Removables(5m, 7m, 9m);
        var sheet = new Sheet(1, 1);
        using var binding = sheet.Bind(target).Rows("A1:A1", static x => x.Lines, static x => x.Column("A", static y => y.Quantity));

        sheet.SetValues(1, 0, [[-1m], [4m]]);

        Assert.Equal([5m, 4m], target.Lines.Select(static x => x.Quantity));
        Assert.Equal(4m, sheet.GetValue(1, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValuesSetOverTwoListsReachTheRowsThatASetterAboveMoves(bool sameBind)
    {
        var target = new HookedLists { Upper = { new Hooked(), new Hooked() }, Lower = { new Hooked(), new Hooked() } };
        target.Upper[0].AfterSet = () => target.Upper.Add(new Hooked());
        var sheet = new Sheet(4, 1);
        using var together = sameBind ? sheet.Bind(target).Rows("A1:A2", static x => x.Upper, static x => x.Column("A", static y => y.Value)).Rows("A3:A4", static x => x.Lower, static x => x.Column("A", static y => y.Value)) : null;
        using var above = sameBind ? null : sheet.Bind(target).Rows("A1:A2", static x => x.Upper, static x => x.Column("A", static y => y.Value));
        using var below = sameBind ? null : sheet.Bind(target).Rows("A3:A4", static x => x.Lower, static x => x.Column("A", static y => y.Value));

        sheet.SetValues(0, 0, [[200m], [1m], [2m], [3m]]);

        Assert.Equal([200m, 1m, 0m], target.Upper.Select(static x => x.Value));
        Assert.Equal([2m, 3m], target.Lower.Select(static x => x.Value));
    }

    [Fact]
    public void ValuesSetOverARowWhoseSetterRemovesItsElementWriteNothingMoreIntoIt()
    {
        var target = Removables(5m, 7m);
        var removed = target.Lines[1];
        var sheet = new Sheet(1, 2);
        using var binding = sheet.Bind(target).Rows("A1:B1", static x => x.Lines, static x => x.Column("A", static y => y.Quantity).Column("B", static y => y.Note));

        sheet.SetValues(1, 0, [[-1m, "x"]]);

        Assert.Equal([5m], target.Lines.Select(static x => x.Quantity));
        Assert.Null(removed.Note);
    }

    [Fact]
    public async Task ACommitWhoseSetterMovesTheElementShowsTheRowsAsTheyAreAndLeavesNothingToUndo()
    {
        var target = Removables(5m, 7m, 9m);
        var sheet = new Sheet(4, 1);
        using var binding = sheet.Bind(target).Rows("A1:A1", static x => x.Lines, static x => x.Column("A", static y => y.Quantity));
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var bridge = new SheetScriptBridge(cut.Instance);

        await cut.InvokeAsync(() => bridge.CommitAsync(2, 0, "500", false, null));
        await cut.InvokeAsync(cut.Instance.UndoAsync);

        Assert.Equal([500m, 5m, 7m], target.Lines.Select(static x => x.Quantity));
        Assert.Equal([500m, 5m, 7m], Enumerable.Range(0, 3).Select(x => sheet.GetValue(x, 0)));
    }

    [Fact]
    public void ANewElementWhoseSetterRemovesItLeavesTheRowBelowAlone()
    {
        var target = Removables(5m, 9m);
        var sheet = new Sheet(2, 1);
        using var binding = sheet.Bind(target).Rows("A1:A2", static x => x.Lines, x =>
        {
            x.Create = () => new Removable { Owner = target.Lines };
            x.Column("A", static y => y.Quantity);
        });
        sheet.InsertRows(1, 1);

        sheet.SetValue(1, 0, -1m);

        Assert.Equal([5m, 9m], target.Lines.Select(static x => x.Quantity));
        Assert.Equal(9m, sheet.GetValue(1, 0));
    }

    [Fact]
    public void ANewElementOnTheLastRowWhoseSetterRemovesItReadsNoOtherRow()
    {
        var target = Removables(5m);
        var sheet = new Sheet(1, 2);
        using var binding = sheet.Bind(target).Rows("A1:B1", static x => x.Lines, x =>
        {
            x.Create = () => new Removable { Owner = target.Lines };
            x.Column("A", static y => y.Quantity).Column("B", static y => y.Note);
        });
        sheet.InsertRows(1, 1);

        sheet.SetValue(1, 0, -1m);

        Assert.Equal([5m], target.Lines.Select(static x => x.Quantity));
        Assert.Equal(1, sheet.RowCount);
    }

    [Fact]
    public void ANewElementKeepsAValueItsPropertyCannotTake()
    {
        var target = new TargetList();
        var sheet = new Sheet(2, 2);
        using var binding = sheet.Bind(target).Rows("A1:B2", static x => x.Items, static x => x.Column("A", static y => y.Count).Column("B", static y => y.Guarded));

        sheet.SetValue(0, 0, 1.5m);

        Assert.Equal(0, Assert.Single(target.Items).Count);
        Assert.Equal(1.5m, sheet.GetValue(0, 0));
        Assert.Equal("ok", sheet.GetValue(0, 1));
    }

    [Fact]
    public void ANewElementKeepsTheValueAndTheMarkOfASetterThatThrows()
    {
        var target = new TargetList();
        var sheet = new Sheet(2, 2);
        using var binding = sheet.Bind(target).Rows("A1:B2", static x => x.Items, static x => x.Column("A", static y => y.Count).Column("B", static y => y.Guarded));

        sheet.SetValue(0, 1, "bad");

        Assert.Equal("ok", Assert.Single(target.Items).Guarded);
        Assert.Equal("bad", sheet.GetValue(0, 1));
        Assert.Equal("Not allowed", sheet.GetError(0, 1));
    }

    private static RemovableTarget Removables(params decimal[] quantities)
    {
        var target = new RemovableTarget();
        foreach (var quantity in quantities)
        {
            target.Lines.Add(new Removable { Owner = target.Lines, Quantity = quantity });
        }

        return target;
    }

    public sealed class Target
    {
        public int Count { get; set; }

        public decimal Amount { get; set; }

        public DateOnly Date { get; set; }

        public TimeOnly Time { get; set; }

        public bool Done { get; set; }

        public Status State { get; set; }

        public string Guarded
        {
            get;
            set => field = value == "bad" ? throw new InvalidOperationException("Not allowed") : value;
        }

        = "ok";
    }

    public sealed class ListTarget
    {
        public ObservableCollection<Line> Lines { get; } = [];
    }

    public sealed class ReadOnlyListTarget(ObservableCollection<Line> lines)
    {
        public ReadOnlyObservableCollection<Line> Lines { get; } = new(lines);
    }

    public sealed class TargetList
    {
        public ObservableCollection<Target> Items { get; } = [];
    }

    public sealed class TwoLists
    {
        public ObservableCollection<Line> Lines { get; } = [];

        public ObservableCollection<Line> Others { get; } = [];
    }

    public sealed class Line
    {
        public string? Name { get; set; }

        public string? Note { get; set; }
    }

    public sealed class DoubleTarget
    {
        public double Ratio { get; set; }
    }

    public sealed class OffsetTarget
    {
        public DateTimeOffset? When { get; set; }
    }

    public sealed class Hooked
    {
        public Action? AfterSet { get; set; }

        public decimal Value
        {
            get;
            set
            {
                field = value;
                AfterSet?.Invoke();
            }
        }
    }

    public sealed class HookedLists
    {
        public ObservableCollection<Hooked> Upper { get; } = [];

        public ObservableCollection<Hooked> Lower { get; } = [];
    }

    public sealed class RemovableTarget
    {
        public ObservableCollection<Removable> Lines { get; } = [];
    }

    public sealed class Removable
    {
        public ObservableCollection<Removable>? Owner { get; init; }

        public decimal Quantity
        {
            get;
            set
            {
                field = value;
                if (value < 0)
                {
                    Owner?.Remove(this);
                }
                else if ((value > 100) && (Owner is { } owner))
                {
                    owner.Move(owner.IndexOf(this), 0);
                }
            }
        }

        public string? Note { get; set; }
    }
}
