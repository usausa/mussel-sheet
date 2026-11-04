namespace MusselSheet;

public sealed class SettingsResolutionTests
{
    [Fact]
    public void CellWithoutSettingsUsesDefaults()
    {
        var sheet = new Sheet(3, 3);

        var settings = sheet.GetSettings(1, 1);

        Assert.Equal(ResolvedCellSettings.Default, settings);
        Assert.Equal(CellKind.Text, settings.Kind);
        Assert.False(settings.IsInputDisabled);
        Assert.Empty(settings.Items);
        Assert.Same(ResolvedCellBorders.None, settings.Style.Borders);
    }

    [Fact]
    public void LaterSettingWinsWhereRangesOverlap()
    {
        var sheet = new Sheet(5, 5);
        sheet.Configure(CellRange.Parse("A1:C3"), c => c.Kind = CellKind.Number);
        sheet.Configure(CellRange.Parse("B2:D4"), c => c.Kind = CellKind.Date);

        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 0).Kind);
        Assert.Equal(CellKind.Date, sheet.GetSettings(1, 1).Kind);
        Assert.Equal(CellKind.Date, sheet.GetSettings(3, 3).Kind);
        Assert.Equal(CellKind.Text, sheet.GetSettings(4, 4).Kind);
    }

    [Fact]
    public void UnsetPropertiesKeepEarlierValues()
    {
        var sheet = new Sheet(5, 5);
        sheet.Configure(CellRange.Columns(1, 1), c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
            c.Required = true;
        });
        sheet.Configure(CellRange.Rows(2, 2), c => c.ReadOnly = true);

        var settings = sheet.GetSettings(2, 1);

        Assert.Equal(CellKind.Number, settings.Kind);
        Assert.Equal("N0", settings.Format);
        Assert.True(settings.Required);
        Assert.True(settings.ReadOnly);
        Assert.True(settings.IsInputDisabled);
        Assert.False(sheet.GetSettings(1, 1).ReadOnly);
    }

    [Fact]
    public void LaterSettingCanTurnAnOptionOff()
    {
        var sheet = new Sheet(3, 3);
        sheet.Configure(CellRange.All, c => c.ReadOnly = true);
        sheet.Configure(CellRange.Cell(1, 1), c => c.ReadOnly = false);

        Assert.True(sheet.GetSettings(0, 0).ReadOnly);
        Assert.False(sheet.GetSettings(1, 1).ReadOnly);
    }

    [Fact]
    public void LabelIsInputDisabled()
    {
        var sheet = new Sheet(2, 2);
        sheet[0, 0].Configure(c => c.Kind = CellKind.Label);

        var settings = sheet.GetSettings(0, 0);

        Assert.True(settings.IsInputDisabled);
        Assert.False(settings.ReadOnly);
        Assert.False(sheet.GetSettings(0, 1).IsInputDisabled);
    }

    [Fact]
    public void StyleMergesPerProperty()
    {
        var sheet = new Sheet(3, 3);
        sheet.Configure(CellRange.All, c =>
        {
            c.Style.FillColor = "#eee";
            c.Style.Bold = true;
        });
        sheet.Configure(CellRange.Cell(1, 1), c =>
        {
            c.Style.FillColor = "#fff";
            c.Style.HorizontalAlignment = HorizontalAlignment.Right;
        });

        var style = sheet.GetSettings(1, 1).Style;

        Assert.Equal("#fff", style.FillColor);
        Assert.True(style.Bold);
        Assert.Equal(HorizontalAlignment.Right, style.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, style.VerticalAlignment);
        Assert.Equal("#eee", sheet.GetSettings(0, 0).Style.FillColor);
    }

    [Fact]
    public void BordersMergePerSide()
    {
        var sheet = new Sheet(3, 3);
        sheet.Configure(CellRange.All, c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
        sheet.Configure(CellRange.Cell(1, 1), c =>
        {
            c.Style.Borders.Top = CellBorder.Thick.WithColor("red");
            c.Style.Borders.Left = CellBorder.None;
        });

        var borders = sheet.GetSettings(1, 1).Style.Borders;

        Assert.Equal(new CellBorder(BorderLineStyle.Thick, "red"), borders.Top);
        Assert.Equal(CellBorder.Thin, borders.Right);
        Assert.Equal(CellBorder.Thin, borders.Bottom);
        Assert.Equal(CellBorder.None, borders.Left);
    }

    [Fact]
    public void SetOutlineDrawsOnlyTheOuterEdges()
    {
        var sheet = new Sheet(5, 5);

        sheet.Range("B2:D4").BorderAround(CellBorder.Medium);

        Assert.Equal(new ResolvedCellBorders(CellBorder.Medium, null, null, CellBorder.Medium), sheet.GetSettings(1, 1).Style.Borders);
        Assert.Equal(new ResolvedCellBorders(null, CellBorder.Medium, CellBorder.Medium, null), sheet.GetSettings(3, 3).Style.Borders);
        Assert.Equal(new ResolvedCellBorders(CellBorder.Medium, null, null, null), sheet.GetSettings(1, 2).Style.Borders);
        Assert.Same(ResolvedCellBorders.None, sheet.GetSettings(2, 2).Style.Borders);
        Assert.Same(ResolvedCellBorders.None, sheet.GetSettings(0, 0).Style.Borders);
    }

    [Fact]
    public void ConfigureCopiesThePatch()
    {
        var sheet = new Sheet(2, 2);
        var patch = new CellSettings { Kind = CellKind.Number };
        patch.Style.Borders.Top = CellBorder.Thin;
        sheet.Configure(CellRange.All, patch);

        patch.Kind = CellKind.Date;
        patch.Style.Borders.Top = CellBorder.Thick;

        var settings = sheet.GetSettings(0, 0);
        Assert.Equal(CellKind.Number, settings.Kind);
        Assert.Equal(CellBorder.Thin, settings.Style.Borders.Top);
    }

    [Fact]
    public void ListItemsCanBeWrittenAsStrings()
    {
        var sheet = new Sheet(1, 1);
        sheet[0, 0].Configure(c =>
        {
            c.Kind = CellKind.List;
            c.Items = ["総務部", new CellListItem("S", "営業部")];
        });

        var items = sheet.GetSettings(0, 0).Items;

        Assert.Equal(2, items.Count);
        Assert.Equal("総務部", items[0].DisplayText);
        Assert.Equal("S", items[1].Value);
        Assert.Equal("営業部", items[1].DisplayText);
    }

    [Fact]
    public void BoundsAcceptNumbersDatesAndTimes()
    {
        var sheet = new Sheet(1, 3);
        sheet[0, 0].Configure(c =>
        {
            c.Minimum = 0;
            c.Maximum = 99.5m;
        });
        sheet[0, 1].Configure(c => c.Maximum = new DateOnly(2026, 12, 31));
        sheet[0, 2].Configure(c => c.Minimum = new TimeOnly(9, 0));

        Assert.Equal(0m, sheet.GetSettings(0, 0).Minimum?.Number);
        Assert.Equal(99.5m, sheet.GetSettings(0, 0).Maximum?.Number);
        Assert.Equal(new DateOnly(2026, 12, 31), sheet.GetSettings(0, 1).Maximum?.Date);
        Assert.Null(sheet.GetSettings(0, 1).Maximum?.Number);
        Assert.Equal(new TimeOnly(9, 0), sheet.GetSettings(0, 2).Minimum?.Time);
    }

    [Fact]
    public void ValidatorIsPassedThrough()
    {
        var sheet = new Sheet(1, 1);
        sheet[0, 0].Configure(c => c.Validator = RejectLongText);

        var validator = sheet.GetSettings(0, 0).Validator;

        Assert.NotNull(validator);
        Assert.Equal("4文字以上は入力できません", validator(new CellValidationContext(sheet, new CellAddress(0, 0), null, "abcd")));
        Assert.Null(validator(new CellValidationContext(sheet, new CellAddress(0, 0), null, "abc")));
    }

    [Fact]
    public void NewRuleIsReflectedImmediately()
    {
        var sheet = new Sheet(2, 2);
        Assert.Equal(CellKind.Text, sheet.GetSettings(0, 0).Kind);

        sheet.Configure(CellRange.All, c => c.Kind = CellKind.Number);

        Assert.Equal(CellKind.Number, sheet.GetSettings(0, 0).Kind);
    }

    [Fact]
    public void CellsWithTheSameSettingsShareOneInstance()
    {
        var sheet = new Sheet(3, 3);
        sheet.Configure(CellRange.Columns(0, 1), c => c.Kind = CellKind.Number);

        Assert.Same(sheet.GetSettings(0, 0), sheet.GetSettings(2, 1));
    }

    [Fact]
    public void GetSettingsRejectsCellsOutsideTheSheet()
    {
        var sheet = new Sheet(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetSettings(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetSettings(0, -1));
    }

    private static string? RejectLongText(CellValidationContext context) => context.Text.Length > 3 ? "4文字以上は入力できません" : null;
}
