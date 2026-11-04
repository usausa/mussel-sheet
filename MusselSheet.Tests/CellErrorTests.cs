namespace MusselSheet;

public sealed class CellErrorTests
{
    [Fact]
    public void ErrorIsKeptWithTheValue()
    {
        var sheet = new Sheet(2, 2);
        sheet.SetValue(0, 0, "abc");

        sheet.SetError(0, 0, "数値で入力してください");

        Assert.Equal("数値で入力してください", sheet.GetError(0, 0));
        Assert.Equal("abc", sheet.GetValue(0, 0));
        Assert.Equal("数値で入力してください", sheet["A1"].Error);
        Assert.Null(sheet.GetError(1, 1));
    }

    [Fact]
    public void ErrorCanBeSetOnAnEmptyCellAndCleared()
    {
        var sheet = new Sheet(1, 1);
        var cell = sheet["A1"];

        cell.Error = "必須です";
        Assert.Equal("必須です", cell.Error);
        Assert.Null(cell.Value);

        cell.Error = null;
        Assert.Null(cell.Error);
    }

    [Fact]
    public void SettingAValueClearsTheError()
    {
        var sheet = new Sheet(1, 2);
        sheet.SetError(0, 0, "エラー");
        sheet.SetError(0, 1, "エラー");

        sheet.SetValue(0, 0, 1m);
        sheet.SetValues(0, 1, [[2m]]);

        Assert.Null(sheet.GetError(0, 0));
        Assert.Null(sheet.GetError(0, 1));
    }

    [Fact]
    public void ClearRemovesTheError()
    {
        var sheet = new Sheet(1, 1);
        sheet.SetValue(0, 0, 1m);
        sheet.SetError(0, 0, "エラー");

        sheet.ClearContents(CellRange.All);

        Assert.Null(sheet.GetError(0, 0));
    }

    [Fact]
    public void ErrorMovesWithItsRow()
    {
        var sheet = new Sheet(3, 1);
        sheet.SetError(1, 0, "エラー");

        sheet.InsertRows(0, 1);

        Assert.Equal("エラー", sheet.GetError(2, 0));
        Assert.Null(sheet.GetError(1, 0));
    }

    [Fact]
    public void ErrorChangeIsReportedOnlyWhenItChanges()
    {
        var sheet = new Sheet(2, 2);
        var events = new List<SheetChangedEventArgs>();
        sheet.Changed += (_, e) => events.Add(e);

        sheet.SetError(1, 1, "エラー");
        sheet.SetError(1, 1, "エラー");
        sheet.SetError(0, 0, null);

        var change = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Errors, change.Kind);
        Assert.Equal(CellRange.Cell(1, 1), change.Range);
    }
}
