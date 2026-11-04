namespace MusselSheet;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Bunit;

public sealed class ObjectBindingTests : BunitContext
{
    public ObjectBindingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void CellsShowAndWriteProperties()
    {
        var order = new TestOrder { Date = new DateOnly(2026, 11, 3), Customer = "青山商事" };
        var sheet = new Sheet(2, 2);
        using var binding = sheet.Bind(order).Cell("B1", x => x.Date).Cell("B2", x => x.Customer);

        Assert.Equal(new DateOnly(2026, 11, 3), sheet.GetValue(0, 1));
        Assert.Equal("青山商事", sheet.GetValue(1, 1));

        sheet.SetValue(1, 1, "赤坂工業");
        Assert.Equal("赤坂工業", order.Customer);

        order.Date = new DateOnly(2027, 1, 5);
        Assert.Equal(new DateOnly(2027, 1, 5), sheet.GetValue(0, 1));
    }

    [Fact]
    public void ValuesAreConvertedToThePropertyType()
    {
        var order = new TestOrder { Count = 3 };
        var sheet = new Sheet(1, 1);
        using var binding = sheet.Bind(order).Cell("A1", x => x.Count);
        Assert.Equal(3, sheet.GetValue(0, 0));

        sheet.SetValue(0, 0, 5m);
        Assert.Equal(5, order.Count);
        Assert.Equal(5m, sheet.GetValue(0, 0));

        sheet.SetValue(0, 0, 1.5m);
        Assert.Equal(5, order.Count);

        sheet.ClearContents(CellRange.Cell(0, 0));
        Assert.Equal(0, order.Count);
        Assert.Equal(0, sheet.GetValue(0, 0));
    }

    [Fact]
    public void RowsShowItemsAndKeepTheFrame()
    {
        var order = Order(("ノート", 10m, 120m));
        var sheet = DetailSheet();
        using var binding = BindDetails(sheet, order);

        Assert.Equal("ノート", sheet.GetValue(3, 0));
        Assert.Equal(10m, sheet.GetValue(3, 1));
        Assert.Equal(1200m, sheet.GetValue(3, 3));
        Assert.Null(sheet.GetValue(4, 0));
        Assert.Null(sheet.GetValue(4, 3));
        Assert.Equal(6, sheet.RowCount);

        order.Lines[0].Quantity = 3m;
        Assert.Equal(3m, sheet.GetValue(3, 1));
        Assert.Equal(360m, sheet.GetValue(3, 3));
    }

    [Fact]
    public void ItemsBeyondTheFrameInsertRowsLikeTheDetail()
    {
        var order = Order(("ノート", 10m, 120m), ("鉛筆", 5m, 50m), ("消しゴム", 1m, 80m));
        var sheet = DetailSheet();
        using var binding = BindDetails(sheet, order);

        Assert.Equal(7, sheet.RowCount);
        Assert.Equal("消しゴム", sheet.GetValue(5, 0));
        Assert.Equal(CellKind.Number, sheet.GetSettings(5, 1).Kind);
        Assert.Equal("合計", sheet.GetValue(6, 2));
    }

    [Fact]
    public void ValuesInAnEmptyRowAddAnItemAtThatPosition()
    {
        var order = Order(("ノート", 10m, 120m));
        var sheet = DetailSheet(frameRows: 3);
        using var binding = BindDetails(sheet, order, frameRows: 3);

        sheet.SetValue(5, 0, "消しゴム");
        Assert.Equal(["ノート", "消しゴム"], order.Lines.Select(x => x.Name));
        Assert.Equal(1m, sheet.GetValue(5, 1));
        Assert.Equal(0m, sheet.GetValue(5, 3));

        sheet.SetValue(4, 1, 2m);
        Assert.Equal(["ノート", null, "消しゴム"], order.Lines.Select(x => x.Name));
        Assert.Equal(2m, order.Lines[1].Quantity);

        order.Lines.RemoveAt(1);
        Assert.Null(sheet.GetValue(5, 0));
        sheet.ClearContents(new CellRange(5, 0, 5, 3));
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void ItemsAddedByTheAppUseTheNextEmptyRow()
    {
        var order = Order(("ノート", 10m, 120m));
        var sheet = DetailSheet();
        using var binding = BindDetails(sheet, order);

        order.Lines.Add(new TestLine { Name = "消しゴム", Quantity = 2m, Price = 50m });
        Assert.Equal(6, sheet.RowCount);
        Assert.Equal("消しゴム", sheet.GetValue(4, 0));
        Assert.Equal(100m, sheet.GetValue(4, 3));

        order.Lines.Insert(0, new TestLine { Name = "定規" });
        Assert.Equal(7, sheet.RowCount);
        Assert.Equal("定規", sheet.GetValue(3, 0));
        Assert.Equal("ノート", sheet.GetValue(4, 0));
        Assert.Equal(CellKind.Number, sheet.GetSettings(3, 1).Kind);
        Assert.Equal(CellKind.Label, sheet.GetSettings(2, 1).Kind);

        order.Lines.RemoveAt(1);
        Assert.Equal(6, sheet.RowCount);
        Assert.Equal("消しゴム", sheet.GetValue(4, 0));

        order.Lines.RemoveAt(0);
        Assert.Equal(6, sheet.RowCount);
        Assert.Equal("消しゴム", sheet.GetValue(3, 0));
        Assert.Null(sheet.GetValue(4, 0));
        Assert.Equal(CellKind.Number, sheet.GetSettings(4, 1).Kind);

        order.Lines[0] = new TestLine { Name = "のり" };
        Assert.Equal("のり", sheet.GetValue(3, 0));
        order.Lines = [new TestLine { Name = "はさみ" }, new TestLine { Name = "テープ" }, new TestLine { Name = "ホチキス" }];
        Assert.Equal(7, sheet.RowCount);
        Assert.Equal("ホチキス", sheet.GetValue(5, 0));
        order.Lines.Clear();
        Assert.Equal(6, sheet.RowCount);
        Assert.Null(sheet.GetValue(3, 0));
    }

    [Fact]
    public void BoundCellsFollowInsertedRows()
    {
        var order = Order(("ノート", 10m, 120m));
        var sheet = DetailSheet();
        using var binding = BindDetails(sheet, order);

        sheet.InsertRows(0, 1);
        order.Customer = "赤坂工業";
        Assert.Equal("赤坂工業", sheet.GetValue(2, 1));
        order.Lines.Add(new TestLine { Name = "消しゴム" });
        Assert.Equal("消しゴム", sheet.GetValue(5, 0));
        sheet.SetValue(4, 1, 7m);
        Assert.Equal(7m, order.Lines[0].Quantity);
    }

    [Fact]
    public void PlainObjectsAreShownAgainByRefresh()
    {
        var order = new PlainOrder { Customer = "青山商事", Lines = [new PlainLine { Name = "ノート" }] };
        var sheet = DetailSheet();
        using var binding = sheet.Bind(order)
            .Cell("B2", x => x.Customer)
            .Rows("A4:D5", x => x.Lines, row => row.Column("A", x => x.Name));

        order.Customer = "赤坂工業";
        order.Lines.Add(new PlainLine { Name = "鉛筆" });
        Assert.Equal("青山商事", sheet.GetValue(1, 1));
        binding.Refresh();
        Assert.Equal("赤坂工業", sheet.GetValue(1, 1));
        Assert.Equal("鉛筆", sheet.GetValue(4, 0));

        sheet.SetValue(3, 0, "ボールペン");
        Assert.Equal("ボールペン", order.Lines[0].Name);
    }

    [Fact]
    public void DisposeStopsTheBinding()
    {
        var order = new TestOrder { Customer = "青山商事" };
        var sheet = new Sheet(2, 2);
        var binding = sheet.Bind(order).Cell("B2", x => x.Customer);
        binding.Dispose();

        sheet.SetValue(1, 1, "赤坂工業");
        order.Customer = "秋葉原電機";
        Assert.Equal("赤坂工業", sheet.GetValue(1, 1));
        Assert.Equal("秋葉原電機", order.Customer);
    }

    [Fact]
    public void WrongBindingsAreRejected()
    {
        var order = Order();
        var sheet = DetailSheet();

        Assert.Throws<ArgumentException>(() => sheet.Bind(order).Cell("A1", x => x.Lines.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Bind(order).Rows("A4:B5", x => x.Lines, row => row.Column("C", x => x.Price)));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Bind(order).Rows("A6:D7", x => x.Lines, row => row.Column("A", x => x.Name)));
        Assert.Throws<InvalidOperationException>(() => sheet.Bind(new FixedOrder()).Rows("A4:D5", x => x.Lines, row => row.Column("A", x => x.Name)));
        using var binding = sheet.Bind(new FixedOrder()).Rows("A4:D5", x => x.Lines, row =>
        {
            row.Create = () => new FixedLine("新しい明細");
            row.Column("A", x => x.Name);
            row.Column("B", x => x.Quantity);
        });
        sheet.SetValue(3, 1, 1m);
        Assert.Equal("新しい明細", sheet.GetValue(3, 0));
    }

    [Fact]
    public async Task InputThatDoesNotFitThePropertyIsAnError()
    {
        var order = new TestOrder { Count = 1 };
        var sheet = new Sheet(2, 1);
        sheet.Range("A1").Configure(c => c.Kind = CellKind.Number);
        using var binding = sheet.Bind(order).Cell("A1", x => x.Count).Cell("A2", x => x.Code);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var ok = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "7", false));
        Assert.True(ok.Ok);
        Assert.Equal(7, order.Count);

        var fraction = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "1.5", false));
        Assert.Equal("整数を入力してください", fraction.Error);
        Assert.Equal(7, order.Count);
        var readOnly = await cut.InvokeAsync(() => cut.Instance.CommitAsync(1, 0, "B-2", false));
        Assert.Equal("入力できないセルです", readOnly.Error);
        Assert.Equal("A-1", order.Code);
    }

    [Fact]
    public async Task DeletedRowsRemoveItemsAndUndoBringsTheSameItemsBack()
    {
        var order = Order(("ノート", 10m, 120m), ("鉛筆", 5m, 50m));
        var sheet = DetailSheet();
        using var binding = BindDetails(sheet, order);
        var cut = RenderMenuSheet(sheet);
        var first = order.Lines[0];

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("deleteRows", 3, 0, 3, 3, 3, 0));
        Assert.Equal(["鉛筆"], order.Lines.Select(x => x.Name));

        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(false));
        Assert.Equal(2, order.Lines.Count);
        Assert.Same(first, order.Lines[0]);
        await cut.InvokeAsync(() => cut.Instance.UndoFromScriptAsync(true));
        Assert.Equal(["鉛筆"], order.Lines.Select(x => x.Name));
    }

    [Fact]
    public async Task InsertedRowsBecomeItemsWhenValuesAreEntered()
    {
        var order = Order(("ノート", 10m, 120m), ("鉛筆", 5m, 50m));
        var sheet = DetailSheet();
        using var binding = BindDetails(sheet, order);
        var cut = RenderMenuSheet(sheet);

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("insertRowsBelow", 3, 0, 3, 3, 3, 0));
        Assert.Equal(2, order.Lines.Count);
        await cut.InvokeAsync(() => cut.Instance.CommitAsync(4, 0, "消しゴム", false));
        Assert.Equal(["ノート", "消しゴム", "鉛筆"], order.Lines.Select(x => x.Name));

        await cut.InvokeAsync(() => cut.Instance.ChangeStructureAsync("insertRowsBelow", 5, 0, 5, 3, 5, 0));
        await cut.InvokeAsync(() => cut.Instance.PasteTextAsync(new CellRange(6, 0, 6, 1), "定規\t4"));
        Assert.Equal(["ノート", "消しゴム", "鉛筆", "定規"], order.Lines.Select(x => x.Name));
        Assert.Equal(4m, order.Lines[3].Quantity);
    }

    private static Sheet DetailSheet(int frameRows = 2)
    {
        var sheet = new Sheet(4 + frameRows, 4);
        sheet.Range("A1:A2").Configure(c => c.Kind = CellKind.Label);
        sheet.Range("B1").Configure(c => c.Kind = CellKind.Date);
        sheet.Range("3:3").Configure(c => c.Kind = CellKind.Label);
        sheet.Range(new CellRange(3, 1, 2 + frameRows, 3)).Configure(c => c.Kind = CellKind.Number);
        sheet.Range(new CellRange(3, 3, 2 + frameRows, 3)).Configure(c => c.ReadOnly = true);
        sheet.Range(new CellRange(3 + frameRows, 0, 3 + frameRows, 3)).Configure(c => c.Kind = CellKind.Label);
        sheet.SetValues(2, 0, [["品名", "数量", "単価", "金額"]]);
        sheet.SetValue(3 + frameRows, 2, "合計");
        return sheet;
    }

    private static SheetBinding<TestOrder> BindDetails(Sheet sheet, TestOrder order, int frameRows = 2) =>
        sheet.Bind(order)
            .Cell("B1", x => x.Date)
            .Cell("B2", x => x.Customer)
            .Rows(new CellRange(3, 0, 2 + frameRows, 3), x => x.Lines, row =>
            {
                row.Column("A", x => x.Name);
                row.Column("B", x => x.Quantity);
                row.Column("C", x => x.Price);
                row.Column("D", x => x.Amount);
            });

    private static TestOrder Order(params (string Name, decimal Quantity, decimal Price)[] lines)
    {
        var order = new TestOrder();
        foreach (var (name, quantity, price) in lines)
        {
            order.Lines.Add(new TestLine { Name = name, Quantity = quantity, Price = price });
        }

        return order;
    }

    private IRenderedComponent<MusselSheetView> RenderMenuSheet(Sheet sheet) =>
        Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.AllowInsertRows, true)
            .Add(x => x.AllowDeleteRows, true));

    private sealed class TestOrder : INotifyPropertyChanged
    {
        public TestOrder()
        {
            Lines = [];
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public DateOnly? Date
        {
            get;
            set
            {
                field = value;
                OnChanged();
            }
        }

        public string? Customer
        {
            get;
            set
            {
                field = value;
                OnChanged();
            }
        }

        public int Count
        {
            get;
            set
            {
                field = value;
                OnChanged();
            }
        }

        public string Code { get; } = "A-1";

        public ObservableCollection<TestLine> Lines
        {
            get;
            set
            {
                field = value;
                OnChanged();
            }
        }

        private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class TestLine : INotifyPropertyChanged
    {
        public TestLine()
        {
            Quantity = 1m;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Name
        {
            get;
            set
            {
                field = value;
                OnChanged();
            }
        }

        public decimal Quantity
        {
            get;
            set
            {
                field = value;
                OnChanged();
                OnChanged(nameof(Amount));
            }
        }

        public decimal Price
        {
            get;
            set
            {
                field = value;
                OnChanged();
                OnChanged(nameof(Amount));
            }
        }

        public decimal Amount => Quantity * Price;

        private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed class PlainOrder
    {
        public string? Customer { get; set; }

        public List<PlainLine> Lines { get; init; } = [];
    }

    private sealed class PlainLine
    {
        public string? Name { get; set; }
    }

    private sealed class FixedOrder
    {
        public List<FixedLine> Lines { get; } = [];
    }

    private sealed class FixedLine(string name)
    {
        public string Name { get; set; } = name;

        public decimal Quantity { get; set; }
    }
}
