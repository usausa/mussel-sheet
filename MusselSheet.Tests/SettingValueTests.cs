namespace MusselSheet;

public sealed class SettingValueTests
{
    [Fact]
    public void BoundHoldsOneKindOfValue()
    {
        CellBound number = 99.5m;
        CellBound date = new DateOnly(2026, 12, 31);
        CellBound time = new TimeOnly(9, 0);

        Assert.Equal(99.5m, number.Number);
        Assert.Null(number.Date);
        Assert.Equal(new DateOnly(2026, 12, 31), date.Date);
        Assert.Null(date.Number);
        Assert.Equal(new TimeOnly(9, 0), time.Time);
        Assert.Equal("99.5", number.ToString());
        Assert.Equal("2026-12-31", date.ToString());
        Assert.Equal("09:00:00", time.ToString());
    }

    [Fact]
    public void ListItemShowsTheValueWhenTextIsMissing()
    {
        CellListItem plain = "総務部";
        var named = new CellListItem("S", "営業部");

        Assert.Equal("総務部", plain.Value);
        Assert.Equal("総務部", plain.DisplayText);
        Assert.Equal("営業部", named.DisplayText);
    }
}
