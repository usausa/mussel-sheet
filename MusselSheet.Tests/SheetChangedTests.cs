namespace MusselSheet;

using System.Globalization;

public sealed class SheetChangedTests
{
    [Fact]
    public void SetValueReportsTheCell()
    {
        var sheet = new Sheet(3, 3);
        var events = Record(sheet);

        sheet.SetValue(1, 2, "x");

        var e = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Values, e.Kind);
        Assert.Equal(CellRange.Cell(1, 2), e.Range);
    }

    [Fact]
    public void SetValuesReportsTheWholeRangeOnce()
    {
        var sheet = new Sheet(3, 3);
        var events = Record(sheet);

        sheet.SetValues(0, 1, [[1, 2], [3]]);

        var e = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Values, e.Kind);
        Assert.Equal(new CellRange(0, 1, 1, 2), e.Range);
    }

    [Fact]
    public void ClearReportsTheClippedRange()
    {
        var sheet = new Sheet(3, 3);
        var events = Record(sheet);

        sheet.Range("B:B").ClearContents();
        sheet.Range("Z1").ClearContents();

        var e = Assert.Single(events);
        Assert.Equal(new CellRange(0, 1, 2, 1), e.Range);
    }

    [Fact]
    public void ConfigureReportsTheRange()
    {
        var sheet = new Sheet(3, 3);
        var events = Record(sheet);

        sheet.Configure(CellRange.Columns(1, 1), c => c.ReadOnly = true);

        var e = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Settings, e.Kind);
        Assert.Equal(CellRange.Columns(1, 1), e.Range);
    }

    [Fact]
    public void StructureChangesReportPositionAndCount()
    {
        var sheet = new Sheet(3, 3);
        var events = Record(sheet);

        sheet.InsertRows(1, 2);
        sheet.DeleteColumns(0, 1);

        Assert.Equal(2, events.Count);
        Assert.Equal(SheetChangeKind.RowsInserted, events[0].Kind);
        Assert.Equal(1, events[0].Index);
        Assert.Equal(2, events[0].Count);
        Assert.Equal(CellRange.Rows(1, 2), events[0].Range);
        Assert.Equal(SheetChangeKind.ColumnsDeleted, events[1].Kind);
        Assert.Equal(0, events[1].Index);
        Assert.Equal(1, events[1].Count);
        Assert.Null(events[1].Range);
    }

    [Fact]
    public void LayoutChangeIsReportedOnlyWhenSomethingChanges()
    {
        var sheet = new Sheet(2, 2);
        var events = Record(sheet);

        sheet.Columns[0].Width = 120;
        sheet.Columns[0].Width = 120;
        sheet.Rows[1].Header = "計";
        sheet.Rows[1].Header = "計";

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(SheetChangeKind.Layout, e.Kind));
    }

    [Fact]
    public void ChangingTheCultureReportsReset()
    {
        var sheet = new Sheet(1, 1);
        var events = Record(sheet);

        sheet.Culture = CultureInfo.InvariantCulture;

        var e = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Reset, e.Kind);
    }

    [Fact]
    public void BeginUpdateReportsOneResetAtTheEnd()
    {
        var sheet = new Sheet(3, 3);
        var events = Record(sheet);

        using (sheet.BeginUpdate())
        {
            sheet.SetValue(0, 0, 1);
            sheet.Configure(CellRange.All, c => c.Kind = CellKind.Number);
            sheet.AddRows(1);

            Assert.Empty(events);
        }

        var e = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Reset, e.Kind);
    }

    [Fact]
    public void NestedBeginUpdateReportsAfterTheOuterScope()
    {
        var sheet = new Sheet(2, 2);
        var events = Record(sheet);

        using (sheet.BeginUpdate())
        {
            using (sheet.BeginUpdate())
            {
                sheet.SetValue(0, 0, 1);
            }

            Assert.Empty(events);
        }

        Assert.Single(events);
    }

    [Fact]
    public void BeginUpdateWithoutChangesReportsNothing()
    {
        var sheet = new Sheet(2, 2);
        var events = Record(sheet);

        sheet.BeginUpdate().Dispose();

        Assert.Empty(events);
    }

    [Fact]
    public void DisposingAScopeTwiceDoesNotBreakLaterScopes()
    {
        var sheet = new Sheet(2, 2);
        var events = Record(sheet);

        var scope = sheet.BeginUpdate();
        scope.Dispose();
        scope.Dispose();

        using (sheet.BeginUpdate())
        {
            sheet.SetValue(0, 0, 1);

            Assert.Empty(events);
        }

        Assert.Single(events);
    }

    [Fact]
    public void SetOutlineReportsOneChange()
    {
        var sheet = new Sheet(3, 3);
        var events = Record(sheet);

        sheet.Range("A1:B2").BorderAround(CellBorder.Thin);

        var e = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Reset, e.Kind);
    }

    private static List<SheetChangedEventArgs> Record(Sheet sheet)
    {
        var events = new List<SheetChangedEventArgs>();
        sheet.Changed += (_, e) => events.Add(e);
        return events;
    }
}
