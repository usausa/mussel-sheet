namespace MusselSheet;

using MusselSheet.Internal;

public sealed class SheetMapTests
{
    [Fact]
    public void RowsWithTheSameRulesShareOnePattern()
    {
        var sheet = new Sheet(100, 3);
        sheet.Configure(CellRange.Columns(1, 1), c => c.Kind = CellKind.Number);
        sheet.Configure(CellRange.Columns(2, 2), c => c.ReadOnly = true);
        sheet.Configure(CellRange.Rows(50, 50), c => c.Kind = CellKind.Label);

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);

        Assert.Equal(2, map.Patterns.Count);
        Assert.Equal(0, map.RowPatterns[0]);
        Assert.Equal(1, map.RowPatterns[50]);
        Assert.Equal(0, map.RowPatterns[99]);
        var normal = map.Patterns[0];
        Assert.Equal("text", map.Rules[normal[0]].Kind);
        Assert.Equal("number", map.Rules[normal[1]].Kind);
        Assert.True(map.Rules[normal[2]].Disabled);
        Assert.False(map.Rules[normal[1]].Disabled);
        Assert.True(map.Rules[map.Patterns[1][1]].Disabled);
    }

    [Fact]
    public void RowsAreGroupedWhereTheApplyingRulesChange()
    {
        // Row bounds fall only at a rule's top row and at the row after its bottom row
        var sheet = new Sheet(10, 3);
        sheet.Configure(CellRange.Rows(2, 4), c => c.Kind = CellKind.Number);
        sheet.Configure(CellRange.Parse("B4:C8"), c => c.ReadOnly = true);
        sheet.Configure(CellRange.Columns(0, 0), c => c.Kind = CellKind.Label);
        sheet.Configure(CellRange.Parse("C10"), c => c.Kind = CellKind.Date);

        int[] bounds = [0, 2, 3, 5, 8, 9, 10];
        Assert.Equal(bounds, SheetMapBuilder.RowBounds(sheet));

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);
        for (var r = 0; r < sheet.RowCount; r++)
        {
            for (var c = 0; c < sheet.ColumnCount; c++)
            {
                var settings = sheet.GetSettings(r, c);
                var rule = map.Rules[map.Patterns[map.RowPatterns[r]][c]];
                Assert.Equal(SheetMapBuilder.KindName(settings.Kind), rule.Kind);
                Assert.Equal(settings.IsInputDisabled, rule.Disabled);
            }
        }
    }

    [Fact]
    public void MapCarriesMergedRanges()
    {
        var sheet = new Sheet(5, 5);
        sheet.Range("B2:C3").Merge();
        sheet.Range("A5:E5").Merge();

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);

        int[] merges = [1, 1, 2, 2, 4, 0, 4, 4];
        Assert.Equal(merges, map.Merges);
    }

    [Fact]
    public void EmptySheetHasNoRowPatterns()
    {
        var map = SheetMapBuilder.Build(new Sheet(0, 3), 24, 40, SheetValidationMode.Stay);

        Assert.Empty(map.RowPatterns);
        Assert.Empty(map.Patterns);
    }

    [Fact]
    public void RulesCarryBoundsItemsAndPolicy()
    {
        var sheet = new Sheet(1, 2);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Minimum = 0;
            c.Maximum = 1000;
            c.Format = "N0";
            c.ValidationMode = SheetValidationMode.AcceptAndMark;
        });
        sheet["B1"].Configure(c =>
        {
            c.Kind = CellKind.List;
            c.Items = [new CellListItem("S", "営業部"), "総務部"];
        });

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);
        var number = map.Rules[map.Patterns[0][0]];
        var list = map.Rules[map.Patterns[0][1]];

        Assert.Equal("0", number.Minimum);
        Assert.Equal("1000", number.Maximum);
        Assert.Equal("1,000", number.MaximumText);
        Assert.Equal("accept", number.Policy);
        Assert.Equal("stay", list.Policy);
        ScriptItem[] items = [new("S", "営業部"), new("総務部", "総務部")];
        Assert.Equal(items, list.Items);
    }

    [Fact]
    public void RulesCarryInputAssistance()
    {
        var sheet = new Sheet(1, 4);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Mask;
            c.Mask = "999-9999";
            c.ImeMode = ImeMode.Off;
        });
        sheet["B1"].Configure(c =>
        {
            c.CharacterRestriction = JapaneseCharacters.Katakana;
        });
        sheet["C1"].Configure(c =>
        {
            c.Kind = CellKind.AutoComplete;
            c.Items = ["東京", "大阪"];
            c.SuggestionProvider = _ => Task.FromResult<IReadOnlyList<string>>([]);
        });
        sheet["D1"].Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Mask = "999";
        });

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);
        var rules = map.Patterns[0].Select(x => map.Rules[x]).ToArray();

        Assert.Equal("999-9999", rules[0].Mask);
        Assert.Equal("half", rules[0].Ime);
        Assert.Equal("katakana", rules[1].Restriction);
        Assert.Null(rules[1].Ime);
        Assert.True(rules[2].Suggest);
        Assert.Equal(2, rules[2].Items?.Count);
        Assert.Null(rules[3].Mask);
        Assert.False(rules[3].Suggest);
    }

    [Fact]
    public void GeometryListsOnlyCustomRowHeights()
    {
        var sheet = new Sheet(3, 2);
        sheet.Rows[1].Height = 40;
        sheet.Columns[0].Width = 80;

        var map = SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay);

        int[] widths = [80, 100];
        int[] heights = [1, 40];
        Assert.Equal(widths, map.ColumnWidths);
        Assert.Equal(heights, map.CustomRowHeights);
        Assert.Equal(24, map.HeaderHeight);
        Assert.Equal(40, map.RowHeaderWidth);
    }
}
