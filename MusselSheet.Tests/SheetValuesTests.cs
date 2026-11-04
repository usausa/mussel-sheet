namespace MusselSheet;

using System.Globalization;

public sealed class SheetValuesTests
{
    [Fact]
    public void SetValueStoresTheValueAsIs()
    {
        var sheet = new Sheet(2, 2);

        sheet.SetValue(1, 1, 42m);

        Assert.Equal(42m, sheet.GetValue(1, 1));
        Assert.Equal(42m, sheet["B2"].Value);
        Assert.Null(sheet.GetValue(0, 0));
    }

    [Fact]
    public void GetValueConvertsToTheRequestedType()
    {
        var sheet = new Sheet(1, 4);
        sheet.SetValues(0, 0, [[42m, new DateTime(2026, 11, 1), new TimeSpan(1, 30, 0)]]);

        Assert.Equal(42, sheet.GetValue<int>(0, 0));
        Assert.Equal(42.0, sheet.GetValue<double>(0, 0));
        Assert.Equal(new DateOnly(2026, 11, 1), sheet.GetValue<DateOnly>(0, 1));
        Assert.Equal(new TimeOnly(1, 30), sheet.GetValue<TimeOnly>(0, 2));
        Assert.Null(sheet.GetValue<int?>(0, 3));
        Assert.Null(sheet.GetValue<string>(0, 3));
    }

    [Fact]
    public void GetValueThrowsWhenTheValueCannotBeConverted()
    {
        var sheet = new Sheet(1, 2);
        sheet.SetValue(0, 0, "abc");

        Assert.Throws<InvalidCastException>(() => sheet.GetValue<int>(0, 0));
        Assert.Throws<InvalidCastException>(() => sheet.GetValue<int>(0, 1));
        Assert.False(sheet.TryGetValue<decimal>(0, 0, out _));
        Assert.True(sheet.TryGetValue<string>(0, 0, out var text));
        Assert.Equal("abc", text);
    }

    [Fact]
    public void DisplayTextFollowsKindAndFormat()
    {
        var sheet = new Sheet(1, 6);
        sheet[0, 0].Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
        });
        sheet[0, 1].Configure(c => c.Kind = CellKind.Date);
        sheet[0, 2].Configure(c => c.Kind = CellKind.Time);
        sheet[0, 3].Configure(c => c.Kind = CellKind.Checkbox);
        sheet[0, 4].Configure(c =>
        {
            c.Kind = CellKind.List;
            c.Items = [new CellListItem("S", "営業部")];
        });

        sheet.SetValues(0, 0, [[1234567m, new DateOnly(2026, 11, 1), new TimeOnly(9, 5), true, "S", "自由入力"]]);

        Assert.Equal("1,234,567", sheet.GetText(0, 0));
        Assert.Equal("2026/11/01", sheet.GetText(0, 1));
        Assert.Equal("09:05", sheet.GetText(0, 2));
        Assert.Equal("☑", sheet.GetText(0, 3));
        Assert.Equal("営業部", sheet.GetText(0, 4));
        Assert.Equal("自由入力", sheet.GetText(0, 5));
    }

    [Fact]
    public void EmptyCellShowsNothingExceptAnUncheckedBox()
    {
        var sheet = new Sheet(1, 2);
        sheet[0, 1].Configure(c => c.Kind = CellKind.Checkbox);

        Assert.Equal(string.Empty, sheet.GetText(0, 0));
        Assert.Equal("☐", sheet.GetText(0, 1));
        Assert.Equal(string.Empty, sheet.GetEditText(0, 1));
    }

    [Fact]
    public void EditTextHasNoFormatUnlessInputTextExists()
    {
        var sheet = new Sheet(1, 4);
        sheet[0, 0].Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
        });
        sheet[0, 1].Configure(c => c.Kind = CellKind.Time);
        sheet[0, 2].Configure(c => c.Kind = CellKind.Checkbox);
        sheet.SetValues(0, 0, [[1234567m, new TimeOnly(9, 5, 30), false]]);
        sheet.SetInput(0, 3, 1200m, "１２００");

        Assert.Equal("1234567", sheet.GetEditText(0, 0));
        Assert.Equal("09:05:30", sheet.GetEditText(0, 1));
        Assert.Equal("false", sheet.GetEditText(0, 2));
        Assert.Equal("１２００", sheet.GetEditText(0, 3));
        Assert.Equal("１２００", sheet.GetInputText(0, 3));
        Assert.Null(sheet.GetInputText(0, 0));
    }

    [Fact]
    public void SetValueClearsTheInputText()
    {
        var sheet = new Sheet(1, 1);
        sheet.SetInput(0, 0, 1m, "1");

        sheet.SetValue(0, 0, 2m);

        Assert.Null(sheet.GetInputText(0, 0));
        Assert.Equal("2", sheet.GetEditText(0, 0));
    }

    [Fact]
    public void DisplayTextUsesTheSheetCulture()
    {
        var sheet = new Sheet(1, 1) { Culture = CultureInfo.GetCultureInfo("de-DE") };
        sheet[0, 0].Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N2";
        });

        sheet.SetValue(0, 0, 1234.5m);

        Assert.Equal("1.234,50", sheet.GetText(0, 0));
        Assert.Equal("ja-JP", new Sheet(1, 1).Culture.Name);
    }

    [Fact]
    public void GetValuesReturnsRowsClippedToTheSheet()
    {
        var sheet = new Sheet(3, 3);
        sheet.SetValues(1, 1, [[1, 2], [3, 4]]);

        var values = sheet.Range("B:C").GetValues();

        object?[][] expected = [[null, null], [1, 2], [3, 4]];
        Assert.Equal(expected, values);
        Assert.Empty(sheet.GetValues(CellRange.Parse("Z100")));
    }

    [Fact]
    public void SetValuesRejectsValuesOutsideTheSheet()
    {
        var sheet = new Sheet(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetValues(1, 1, [[1, 2]]));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetValues(1, 0, [[1], [2]]));
        Assert.Null(sheet.GetValue(1, 1));
    }

    [Fact]
    public void ClearRemovesValuesButKeepsSettings()
    {
        var sheet = new Sheet(2, 2);
        sheet.Configure(CellRange.All, c => c.Kind = CellKind.Number);
        sheet.SetValues(0, 0, [[1, 2], [3, 4]]);

        sheet.Range("A1:B1").ClearContents();

        Assert.Null(sheet.GetValue(0, 0));
        Assert.Null(sheet.GetValue(0, 1));
        Assert.Equal(3, sheet.GetValue(1, 0));
        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 0).Kind);
    }

    [Fact]
    public void CellReadsAndWritesThroughTheSheet()
    {
        var sheet = new Sheet(3, 3);
        var cell = sheet["C2"];
        cell.Configure(c => c.Kind = CellKind.Number);

        cell.Value = 10m;

        Assert.Equal(new CellAddress(1, 2), cell.Address);
        Assert.Equal(10m, sheet.GetValue(1, 2));
        Assert.Equal(10, cell.GetValue<int>());
        Assert.Equal("10", cell.Text);
        Assert.Equal(CellKind.Number, cell.Settings.Kind);
        Assert.Equal("C2", cell.ToString());
    }
}
