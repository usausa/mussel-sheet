namespace MusselSheet;

public sealed class SheetStructureTests
{
    [Fact]
    public void InsertRowsMovesValuesDown()
    {
        var sheet = new Sheet(3, 2);
        sheet.SetValues(0, 0, [["a"], ["b"], ["c"]]);

        sheet.InsertRows(1, 2);

        Assert.Equal(5, sheet.RowCount);
        Assert.Equal("a", sheet.GetValue(0, 0));
        Assert.Null(sheet.GetValue(1, 0));
        Assert.Null(sheet.GetValue(2, 0));
        Assert.Equal("b", sheet.GetValue(3, 0));
        Assert.Equal("c", sheet.GetValue(4, 0));
    }

    [Fact]
    public void InsertRowsMovesRulesBelowTheInsertPoint()
    {
        var sheet = new Sheet(5, 2);
        sheet.Configure(CellRange.Parse("A3:B4"), c => c.Kind = CellKind.Number);

        sheet.InsertRows(1, 1);

        Assert.Equal(CellKind.Text, sheet.GetSettings(1, 0).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(2, 0).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(3, 0).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(4, 1).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(5, 0).Kind);
    }

    [Fact]
    public void InsertRowsInheritsTheRowAbove()
    {
        var sheet = new Sheet(3, 3);
        sheet.Configure(CellRange.Parse("A2:C2"), c => c.Kind = CellKind.Number);
        sheet.Configure(CellRange.Parse("B2"), c => c.ReadOnly = true);
        sheet.Rows[1].Height = 40;

        sheet.InsertRows(2, 2);

        Assert.Equal(CellKind.Number, sheet.GetSettings(2, 0).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(3, 2).Kind);
        Assert.True(sheet.GetSettings(3, 1).ReadOnly);
        Assert.False(sheet.GetSettings(3, 0).ReadOnly);
        Assert.Equal(40, sheet.Rows[3].Height);
        Assert.Equal(CellKind.Text, sheet.GetSettings(4, 0).Kind);
    }

    [Fact]
    public void InheritedRulesKeepTheirPriority()
    {
        var sheet = new Sheet(2, 1);
        sheet.Configure(CellRange.Parse("A1"), c => c.Kind = CellKind.Number);
        sheet.Configure(CellRange.Parse("A1:A2"), c => c.Kind = CellKind.Date);

        sheet.InsertRows(1, 1);

        Assert.Equal(CellKind.Date, sheet.GetSettings(0, 0).Kind);
        Assert.Equal(CellKind.Date, sheet.GetSettings(1, 0).Kind);
    }

    [Fact]
    public void InsertRowsWithoutInheritLeavesNewRowsUnset()
    {
        var sheet = new Sheet(3, 3);
        sheet.Configure(CellRange.Parse("A2:C2"), c => c.Kind = CellKind.Number);
        sheet.Rows[1].Height = 40;

        sheet.InsertRows(2, 1, inheritSettings: false);

        Assert.Equal(CellKind.Number, sheet.GetSettings(1, 0).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(2, 0).Kind);
        Assert.Null(sheet.Rows[2].Height);
    }

    [Fact]
    public void InsertRowsInsideARangeExtendsItWhenInheriting()
    {
        var sheet = new Sheet(5, 1);
        sheet.Configure(CellRange.Parse("A2:A4"), c => c.Kind = CellKind.Number);

        sheet.InsertRows(2, 1);

        for (var row = 1; row <= 4; row++)
        {
            Assert.Equal(CellKind.Number, sheet.GetSettings(row, 0).Kind);
        }

        Assert.Equal(CellKind.Text, sheet.GetSettings(5, 0).Kind);
    }

    [Fact]
    public void InsertRowsInsideARangeSplitsItWhenNotInheriting()
    {
        var sheet = new Sheet(5, 1);
        sheet.Configure(CellRange.Parse("A2:A4"), c => c.Kind = CellKind.Number);

        sheet.InsertRows(2, 1, inheritSettings: false);

        Assert.Equal(CellKind.Number, sheet.GetSettings(1, 0).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(2, 0).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(3, 0).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(4, 0).Kind);
        Assert.Equal(2, sheet.Rules.Count);
    }

    [Fact]
    public void EntireColumnRuleCoversInsertedRows()
    {
        var sheet = new Sheet(3, 2);
        sheet.Configure(CellRange.Columns(1, 1), c => c.Kind = CellKind.Number);

        sheet.InsertRows(1, 1, inheritSettings: false);

        Assert.Equal(CellKind.Number, sheet.GetSettings(1, 1).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(1, 0).Kind);
    }

    [Fact]
    public void InsertRowsAtTheTopInheritsTheRowBelow()
    {
        var sheet = new Sheet(2, 1);
        sheet.Configure(CellRange.Parse("A1"), c => c.Kind = CellKind.Date);
        sheet.Rows[0].Height = 40;

        sheet.InsertRows(0, 1);

        Assert.Equal(CellKind.Date, sheet.GetSettings(0, 0).Kind);
        Assert.Equal(CellKind.Date, sheet.GetSettings(1, 0).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(2, 0).Kind);
        Assert.Equal(40, sheet.Rows[0].Height);
    }

    [Fact]
    public void AddRowsExtendsTheLastRow()
    {
        var sheet = new Sheet(2, 2);
        sheet.Configure(CellRange.Parse("B2"), c => c.Kind = CellKind.Checkbox);
        sheet.Rows[1].Height = 32;

        sheet.AddRows(3);

        Assert.Equal(5, sheet.RowCount);
        Assert.Equal(CellKind.Checkbox, sheet.GetSettings(4, 1).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(4, 0).Kind);
        Assert.Equal(32, sheet.Rows[4].ActualHeight);
        Assert.Equal("5", sheet.Rows[4].HeaderText);
    }

    [Fact]
    public void AddRowsToAnEmptySheet()
    {
        var sheet = new Sheet(0, 2);

        sheet.AddRows(2);
        sheet.SetValue(1, 1, 5);

        Assert.Equal(2, sheet.RowCount);
        Assert.Equal(5, sheet.GetValue(1, 1));
        Assert.Equal(sheet.DefaultRowHeight, sheet.Rows[1].ActualHeight);
    }

    [Fact]
    public void RemoveRowsMovesValuesAndRulesUp()
    {
        var sheet = new Sheet(6, 1);
        sheet.SetValues(0, 0, [[0], [1], [2], [3], [4], [5]]);
        sheet.Configure(CellRange.Parse("A5:A6"), c => c.Kind = CellKind.Number);

        sheet.DeleteRows(1, 2);

        Assert.Equal(4, sheet.RowCount);
        Assert.Equal(0, sheet.GetValue(0, 0));
        Assert.Equal(3, sheet.GetValue(1, 0));
        Assert.Equal(CellKind.Text, sheet.GetSettings(1, 0).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(2, 0).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(3, 0).Kind);
    }

    [Fact]
    public void RemoveRowsShrinksOrDropsOverlappingRules()
    {
        var sheet = new Sheet(6, 1);
        sheet.Configure(CellRange.Parse("A2:A4"), c => c.Kind = CellKind.Number);
        sheet.Configure(CellRange.Parse("A4:A6"), c => c.ReadOnly = true);
        sheet.Configure(CellRange.Parse("A3"), c => c.Required = true);

        sheet.DeleteRows(2, 2);

        Assert.Equal(CellKind.Number, sheet.GetSettings(1, 0).Kind);
        Assert.False(sheet.GetSettings(1, 0).ReadOnly);
        Assert.Equal(CellKind.Text, sheet.GetSettings(2, 0).Kind);
        Assert.True(sheet.GetSettings(2, 0).ReadOnly);
        Assert.True(sheet.GetSettings(3, 0).ReadOnly);
        Assert.False(sheet.GetSettings(2, 0).Required);
        Assert.Equal(2, sheet.Rules.Count);
    }

    [Fact]
    public void InsertColumnsMovesValuesAndInheritsTheLeftColumn()
    {
        var sheet = new Sheet(2, 3);
        sheet.SetValues(0, 1, [["b", "c"]]);
        sheet.Configure(CellRange.Columns(1, 1), c => c.Kind = CellKind.Number);
        sheet.Columns[1].Width = 150;

        sheet.InsertColumns(2, 1);

        Assert.Equal(4, sheet.ColumnCount);
        Assert.Equal("b", sheet.GetValue(0, 1));
        Assert.Null(sheet.GetValue(0, 2));
        Assert.Equal("c", sheet.GetValue(0, 3));
        Assert.Equal(CellKind.Number, sheet.GetSettings(1, 2).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(1, 3).Kind);
        Assert.Equal(150, sheet.Columns[2].Width);
        Assert.Equal("C", sheet.Columns[2].HeaderText);
        Assert.Equal("D", sheet.Columns[3].HeaderText);
    }

    [Fact]
    public void InsertColumnsWithoutInheritSplitsASpanningRange()
    {
        var sheet = new Sheet(2, 4);
        sheet.Configure(CellRange.Parse("A1:C1"), c => c.Kind = CellKind.Number);
        sheet.Configure(CellRange.Rows(1, 1), c => c.ReadOnly = true);

        sheet.InsertColumns(1, 1, inheritSettings: false);

        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 0).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(0, 1).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 2).Kind);
        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 3).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(0, 4).Kind);
        Assert.True(sheet.GetSettings(1, 1).ReadOnly);
        Assert.Null(sheet.Columns[1].Width);
    }

    [Fact]
    public void RemoveColumnsMovesValuesAndRulesLeft()
    {
        var sheet = new Sheet(1, 4);
        sheet.SetValues(0, 0, [["a", "b", "c", "d"]]);
        sheet.Configure(CellRange.Parse("D1"), c => c.Kind = CellKind.Number);

        sheet.DeleteColumns(1, 2);

        Assert.Equal(2, sheet.ColumnCount);
        Assert.Equal("a", sheet.GetValue(0, 0));
        Assert.Equal("d", sheet.GetValue(0, 1));
        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 1).Kind);
        Assert.Equal("B", sheet.Columns[1].HeaderText);
    }

    [Fact]
    public void CustomHeaderMovesWithItsRow()
    {
        var sheet = new Sheet(3, 1);
        sheet.Rows[1].Header = "合計";

        sheet.InsertRows(0, 1);

        Assert.Equal("1", sheet.Rows[0].HeaderText);
        Assert.Equal("合計", sheet.Rows[2].HeaderText);
        Assert.Equal("4", sheet.Rows[3].HeaderText);
    }

    [Fact]
    public void PutRowsRestoresTheSameObjectsWithTheirValues()
    {
        var sheet = new Sheet(3, 2);
        sheet.Range("3:3").Configure(c => c.Kind = CellKind.Number);
        var row = sheet.Rows[2];
        sheet.SetValue(2, 0, "x");
        sheet.Rows[2].Height = 40;
        var rules = sheet.Rules.ToArray();
        sheet.DeleteRows(2, 1);
        Assert.Empty(sheet.Rules);

        sheet.PutRows(2, [row], rules, []);

        Assert.Same(row, sheet.Rows[2]);
        Assert.Equal(2, row.Index);
        Assert.Equal("x", sheet.GetValue(2, 0));
        Assert.Equal(40, sheet.Rows[2].Height);
        Assert.Equal(CellKind.Number, sheet.GetSettings(2, 1).Kind);
    }

    [Fact]
    public void PutRowsDropsValuesWhenColumnsChanged()
    {
        var sheet = new Sheet(2, 1);
        var row = sheet.Rows[1];
        sheet.SetValue(1, 0, "x");
        sheet.DeleteRows(1, 1);
        sheet.AddColumns(1);

        sheet.PutRows(1, [row], sheet.Rules.ToArray(), []);

        Assert.Null(sheet.GetValue(1, 0));
        Assert.Null(sheet.GetValue(1, 1));
    }

    [Fact]
    public void TakenColumnsCanBePutBackWithTheirCells()
    {
        var sheet = new Sheet(2, 3);
        sheet.SetValues(0, 0, [["a", "b", "c"], ["d", null, "f"]]);
        sheet.SetError(1, 1, "印");
        sheet.Range("B:B").Configure(c => c.Kind = CellKind.Number);
        var column = sheet.Columns[1];
        var rules = sheet.Rules.ToArray();

        var cells = sheet.TakeColumns(1, 1, [], []);

        Assert.Equal(2, sheet.ColumnCount);
        Assert.Equal("c", sheet.GetValue(0, 1));
        Assert.Equal(2, cells.Count);

        sheet.PutColumns(1, [column], cells, rules, []);

        Assert.Same(column, sheet.Columns[1]);
        Assert.Equal("b", sheet.GetValue(0, 1));
        Assert.Equal("印", sheet.GetError(1, 1));
        Assert.Equal("f", sheet.GetValue(1, 2));
        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 1).Kind);
    }
}
