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
    public void AColumnMustBeInsideTheRows()
    {
        var sheet = new Sheet(2, 3);

        Assert.Throws<ArgumentException>(() => sheet.Bind(new ListTarget()).Rows("A1:B2", static x => x.Lines, static x => x.Column("?", static y => y.Name)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Bind(new ListTarget()).Rows("A1:B2", static x => x.Lines, static x => x.Column("C", static y => y.Name)));
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

    public sealed class Line
    {
        public string? Name { get; set; }

        public string? Note { get; set; }
    }
}
