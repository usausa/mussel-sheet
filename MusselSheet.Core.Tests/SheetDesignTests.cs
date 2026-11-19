namespace MusselSheet;

using System.Text.Json;

public sealed class SheetDesignTests
{
    [Fact]
    public void ADesignWithAPropertyTheLibraryDoesNotKnowIsAnError()
    {
        var json = SheetDesignJson.Write(new Sheet(1, 1).ToDesign());
        var unknown = json.Replace("\"version\"", "\"characterConversions\": \"hiraganaToKatakana\", \"version\"", StringComparison.Ordinal);

        Assert.NotEqual(json, unknown);
        Assert.Throws<JsonException>(() => SheetDesignJson.Read(unknown));
    }

    [Fact]
    public void DesignRoundTripsThroughJson()
    {
        var sheet = DesignedSheet();

        var json = SheetDesignJson.Write(sheet.ToDesign());
        var copy = Sheet.FromDesign(SheetDesignJson.Read(json));

        Assert.Equal((sheet.RowCount, sheet.ColumnCount, 28, 90), (copy.RowCount, copy.ColumnCount, copy.DefaultRowHeight, copy.DefaultColumnWidth));
        Assert.Equal((2, 1), (copy.FrozenRows, copy.FrozenColumns));
        Assert.Equal((40, "表題"), (copy.Rows[0].Height, copy.Rows[0].Header));
        Assert.Equal((160, "品名"), (copy.Columns[0].Width, copy.Columns[0].Header));
        Assert.Equal("ja-JP", copy.Culture.Name);
        Assert.Equal(sheet.MergeAreas, copy.MergeAreas);

        for (var r = 0; r < sheet.RowCount; r++)
        {
            for (var c = 0; c < sheet.ColumnCount; c++)
            {
                var (expected, actual) = (sheet.GetSettings(r, c), copy.GetSettings(r, c));
                Assert.Equal(expected with { Items = actual.Items }, actual);
                Assert.Equal(expected.Items, actual.Items);
            }
        }

        Assert.Equal("経費精算書", copy.GetValue(0, 0));
        Assert.Equal("品名", copy.GetValue(1, 0));
        Assert.Null(copy.GetValue(2, 1));
    }

    [Fact]
    public void JsonIsReadable()
    {
        var json = SheetDesignJson.Write(DesignedSheet().ToDesign());

        Assert.Contains("\"range\": \"B3:C5\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pattern\": \"^[^<>]+$\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"number\"", json, StringComparison.Ordinal);
        Assert.Contains("\"frozenRows\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"characterRestriction\": \"halfwidth\"", json, StringComparison.Ordinal);
        Assert.Contains("\"style\": \"doubleLine\"", json, StringComparison.Ordinal);
        Assert.Contains("\"経費精算書\"", json, StringComparison.Ordinal);
        Assert.Contains("\"merges\": [", json, StringComparison.Ordinal);
        Assert.DoesNotContain("null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AllValuesCanBeIncluded()
    {
        var sheet = DesignedSheet();
        var copy = Sheet.FromDesign(SheetDesignJson.Read(SheetDesignJson.Write(sheet.ToDesign(includeValues: true))));

        Assert.Equal(12m, copy.GetValue(2, 1));
        Assert.Equal(new DateOnly(2026, 11, 3), copy.GetValue(5, 1));
        Assert.Equal(new TimeOnly(9, 30), copy.GetValue(5, 2));
        Assert.Equal(true, copy.GetValue(5, 3));
        Assert.Equal("ノート", copy.GetValue(2, 0));
    }

    [Fact]
    public void RulesWithOpenEndsAreSavedAndRead()
    {
        var sheet = new Sheet(4, 3);
        sheet.Range("2:*").Configure(c => c.Kind = CellKind.Number);
        sheet.Cells.Configure(c => c.ReadOnly = true);

        var copy = Sheet.FromDesign(SheetDesignJson.Read(SheetDesignJson.Write(sheet.ToDesign())));
        copy.AddRows(1);

        Assert.Equal(CellKind.Number, copy.GetSettings(4, 2).Kind);
        Assert.NotEqual(CellKind.Number, copy.GetSettings(0, 0).Kind);
        Assert.True(copy.GetSettings(4, 2).ReadOnly);
    }

    [Fact]
    public void ValuesThatTheDesignCannotHoldAreSavedAsText()
    {
        var sheet = new Sheet(1, 5);
        sheet.SetValue(0, 0, TimeSpan.FromHours(26));
        sheet.SetValue(0, 1, TimeSpan.FromHours(-1));
        sheet.SetValue(0, 2, Double.NaN);
        sheet.SetValue(0, 3, 1e30);
        sheet.SetValue(0, 4, 0.1f);

        var copy = Sheet.FromDesign(SheetDesignJson.Read(SheetDesignJson.Write(sheet.ToDesign(includeValues: true))));

        Assert.Equal("1.02:00:00", copy.GetValue(0, 0));
        Assert.Equal("-01:00:00", copy.GetValue(0, 1));
        Assert.Equal("NaN", copy.GetValue(0, 2));
        Assert.Equal("1E+30", copy.GetValue(0, 3));
        Assert.Equal(0.1m, copy.GetValue(0, 4));
    }

    [Fact]
    public void DoublesAndFloatsAreSavedWithTheDigitsThatReadBackAsTheSameValue()
    {
        var sheet = new Sheet(1, 2);
        sheet.SetValue(0, 0, 0.1 + 0.2);
        sheet.SetValue(0, 1, 1f / 3);

        var copy = Sheet.FromDesign(SheetDesignJson.Read(SheetDesignJson.Write(sheet.ToDesign(includeValues: true))));

        Assert.Equal(0.30000000000000004m, copy.GetValue(0, 0));
        Assert.Equal(0.1 + 0.2, copy.GetValue<double>(0, 0));
        Assert.Equal(0.33333334m, copy.GetValue(0, 1));
        Assert.Equal(1f / 3, copy.GetValue<float>(0, 1));
    }

    [Fact]
    public void DatesAndTimesAreSavedAsDateAndTimeCellsHoldThem()
    {
        var sheet = new Sheet(1, 3);
        sheet.SetValue(0, 0, new DateTime(2026, 11, 18, 10, 30, 15));
        sheet.SetValue(0, 1, new TimeOnly(9, 30, 15, 500));
        sheet.SetValue(0, 2, new TimeSpan(0, 9, 30, 15, 500));

        var copy = Sheet.FromDesign(SheetDesignJson.Read(SheetDesignJson.Write(sheet.ToDesign(includeValues: true))));

        Assert.Equal(new DateOnly(2026, 11, 18), copy.GetValue(0, 0));
        Assert.Equal(new TimeOnly(9, 30, 15), copy.GetValue(0, 1));
        Assert.Equal(new TimeOnly(9, 30, 15), copy.GetValue(0, 2));
    }

    [Fact]
    public void ComputationsWhoseCellsWereDeletedAreNotSaved()
    {
        var functions = new SheetFunctions();
        functions.AddComputation("写し", x => x.Get<decimal?>("B"));
        functions.AddComputation("写し2", x => x.Get<decimal?>("C"));
        var sheet = new Sheet(4, 5);
        sheet.ComputeRows("D2:D4", ["B"], functions.Computation("写し"));
        sheet.ComputeRows("E2:E4", ["C"], functions.Computation("写し2"));

        sheet.DeleteColumns(2, 2);
        var design = sheet.ToDesign(functions);

        Assert.Empty(design.Computations ?? []);
    }

    [Fact]
    public void ComputationsWhoseRowsHaveAGapAreNotSaved()
    {
        var functions = new SheetFunctions();
        functions.AddComputation("写し", x => x.Get<decimal?>("B"));
        var sheet = new Sheet(8, 4);
        sheet.ComputeRows("D4:D6", ["B"], functions.Computation("写し"));
        var total = sheet.Rows[6];
        var rules = sheet.Rules.ToArray();

        sheet.DeleteRows(6, 1);
        sheet.InsertRows(6, 1);
        sheet.PutRows(6, [total], rules, []);
        var design = sheet.ToDesign(functions);

        Assert.Empty(design.Computations ?? []);
    }

    [Fact]
    public void FunctionsAreSavedByName()
    {
        var functions = new SheetFunctions();
        functions.AddValidator("偶数", x => (x.Value is decimal d) && (d % 2 != 0) ? "偶数を入力してください" : null);
        functions.AddFormatter("円", x => x.Value is null ? null : $"{x.Text} 円");
        functions.AddComputation("金額", x => x.Get<decimal?>("B") * x.Get<decimal?>("C") * x.Get<decimal>("D1"));
        functions.AddComputation("合計", x => x.Values("D2:D4").OfType<decimal>().Sum());

        var sheet = new Sheet(5, 4);
        sheet.SetValue(0, 3, 1m);
        sheet.Range("B2:B4").Configure(c => c.Validator = functions.Validator("偶数"));
        sheet.Range("D5").Configure(c => c.Formatter = functions.Formatter("円"));
        sheet.Range("C2").Configure(c => c.Corrector = x => x.Value);
        sheet.ComputeRows("D2:D4", ["B", "C", "D1"], functions.Computation("金額"));
        sheet.Compute("D5", ["D2:D4"], functions.Computation("合計"));

        var design = sheet.ToDesign(functions);
        Assert.Contains(design.Rules!, x => x.Validator == "偶数");
        Assert.DoesNotContain(design.Rules!, x => x.Corrector is not null);
        Assert.Equal(["金額", "合計"], design.Computations!.Select(x => x.Function));

        var copy = Sheet.FromDesign(design, functions);
        Assert.Same(functions.Validator("偶数"), copy.GetSettings(1, 1).Validator);
        copy.SetValue(0, 3, 1m);
        copy.SetValues(1, 1, [[2m, 300m]]);
        Assert.Equal(600m, copy.GetValue(1, 3));
        Assert.Equal(600m, copy.GetValue(4, 3));
        Assert.Equal("600 円", copy.GetText(4, 3));
        Assert.True(copy.GetSettings(4, 3).ReadOnly);
        Assert.Equal(sheet.Rules.Count, copy.Rules.Count);

        Assert.Throws<InvalidOperationException>(() => Sheet.FromDesign(design));
        Assert.Throws<KeyNotFoundException>(() => Sheet.FromDesign(design, new SheetFunctions()));
    }

    [Fact]
    public void ComputationsKeepTheirNamesAfterTheSheetChanges()
    {
        var functions = new SheetFunctions();
        functions.AddComputation("合計", x => x.Values("D2:D3").OfType<decimal>().Sum());
        var sheet = new Sheet(4, 4);
        sheet.SetValues(1, 3, [[100m], [200m]]);
        sheet.Compute("D4", ["D2:D3"], functions.Computation("合計"));

        sheet.InsertRows(3, 1);
        var design = sheet.ToDesign(functions, includeValues: true);
        var computation = Assert.Single(design.Computations!);
        Assert.Equal("D5", computation.Target);
        Assert.Equal(("D2:D3", "D2:D4"), (computation.Observed[0].Name, computation.Observed[0].Range));

        var copy = Sheet.FromDesign(SheetDesignJson.Read(SheetDesignJson.Write(design)), functions);
        copy.SetValue(3, 3, 50m);
        Assert.Equal(350m, copy.GetValue(4, 3));
    }

    [Fact]
    public void AComputationWhoseReadColumnsWereSplitIsNotSaved()
    {
        var functions = new SheetFunctions();
        functions.AddComputation("合計", x => x.Values("A1:B1").OfType<decimal>().Sum());
        var sheet = new Sheet(2, 3);
        sheet.Compute("A2", ["A1:B1"], functions.Computation("合計"));

        sheet.InsertColumns(1, 1);

        Assert.Null(sheet.ToDesign(functions).Computations);
    }

    [Fact]
    public void InvariantCultureIsKept()
    {
        // Invariant culture is saved as an empty name; leaving it out would load as the default culture
        var sheet = new Sheet(1, 1) { Culture = CultureInfo.InvariantCulture };
        var design = sheet.ToDesign();

        Assert.Equal(string.Empty, design.Culture);
        Assert.Equal(CultureInfo.InvariantCulture, Sheet.FromDesign(design).Culture);
    }

    [Fact]
    public void UnknownVersionsAreRejected()
    {
        Assert.Throws<NotSupportedException>(() => Sheet.FromDesign(new SheetDesign { Version = 2, RowCount = 1, ColumnCount = 1 }));
    }

    private static Sheet DesignedSheet()
    {
        var sheet = new Sheet(6, 4) { DefaultRowHeight = 28, DefaultColumnWidth = 90, Culture = CultureInfo.GetCultureInfo("ja-JP"), FrozenRows = 2, FrozenColumns = 1 };
        sheet.Rows[0].Height = 40;
        sheet.Rows[0].Header = "表題";
        sheet.Columns[0].Width = 160;
        sheet.Columns[0].Header = "品名";
        sheet.Range("A1:D1").Merge();
        sheet.Range("A1:D2").Configure(c =>
        {
            c.Kind = CellKind.Label;
            c.Style.Bold = true;
            c.Style.FontSize = 20;
            c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            c.Style.FillColor = "#eef2f7";
            c.Style.Borders = CellBorders.All(CellBorder.Thin.WithColor("#57606a"));
        });
        sheet.Range("A2:D2").Configure(c => c.Style.Borders.Bottom = CellBorder.DoubleLine);
        sheet.Range("B3:C5").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
            c.Minimum = 0;
            c.Maximum = 1000.5m;
            c.ValidationMode = SheetValidationMode.AcceptAndMark;
            c.Style.Italic = true;
            c.Style.IndentLevel = 1;
        });
        sheet.Range("A3:A5").Configure(c =>
        {
            c.MaxLength = 20;
            c.Pattern = "^[^<>]+$";
            c.InputMessage = "品名";
            c.MoveGroup = "明細";
        });
        sheet.Range("D3:D5").Configure(c =>
        {
            c.Kind = CellKind.List;
            c.Items = [new CellListItem("1", "承認済"), "未承認"];
            c.Required = true;
            c.ErrorMessage = "選んでください";
            c.Style.Underline = true;
            c.Style.Strikethrough = true;
        });
        sheet.Range("B6").Configure(c =>
        {
            c.Kind = CellKind.Date;
            c.Minimum = new DateOnly(2026, 1, 1);
        });
        sheet.Range("C6").Configure(c =>
        {
            c.Kind = CellKind.Time;
            c.Maximum = new TimeOnly(18, 0);
            c.ImeMode = ImeMode.Off;
        });
        sheet.Range("D6").Configure(c =>
        {
            c.Kind = CellKind.Checkbox;
            c.CheckedText = "済";
            c.UncheckedText = "未";
        });
        sheet.Range("A6").Configure(c =>
        {
            c.Mask = "999-9999";
            c.CharacterRestriction = SheetCharacters.Halfwidth;
            c.WrapText = false;
            c.Style.CssClass = "zip";
            c.Style.VerticalAlignment = VerticalAlignment.Top;
            c.Style.FontColor = "#c62828";
        });
        sheet.SetValues(0, 0, [
            ["経費精算書"],
            ["品名", "数量", "単価", "状態"],
            ["ノート", 12m, 120m, "1"]
        ]);
        sheet.SetValues(5, 1, [[new DateOnly(2026, 11, 3), new TimeOnly(9, 30), true]]);
        return sheet;
    }
}
