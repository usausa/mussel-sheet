namespace MusselSheet;

public sealed class ApiExampleTests
{
    [Fact]
    public void QuickStartWorks()
    {
        var sheet = new Sheet(5, 2);
        sheet.Columns[0].Header = "Item";
        sheet.Columns[1].Header = "Quantity";
        sheet.Range("B:B").Configure(static c =>
        {
            c.Kind = CellKind.Number;
            c.Minimum = 0;
        });
        sheet.SetValues(0, 0, [["Notebook", 10m]]);

        Assert.Equal("Notebook", sheet.GetValue(0, 0));
        Assert.Equal("10", sheet.GetText(0, 1));
    }

    [Fact]
    public void SpecificationExampleWorks()
    {
        var sheet = new Sheet(rows: 10, columns: 4);
        sheet.Columns[0].Width = 100;
        sheet.Columns[0].Header = "項目";

        sheet.Range("A1:A10").Configure(c =>
        {
            c.Kind = CellKind.Label;
            c.Style.Bold = true;
            c.Style.FillColor = "#f3f3f3";
        });
        sheet.Range("A3:D10").Configure(c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
        sheet.Range("A3:D10").BorderAround(CellBorder.Medium);
        sheet.Range("B1").Configure(c =>
        {
            c.Kind = CellKind.Date;
            c.Format = "yyyy/MM/dd";
        });
        sheet.Range("B2").Configure(c =>
        {
            c.Kind = CellKind.Text;
            c.MaxLength = 20;
        });
        sheet.Range("D1").Configure(c =>
        {
            c.Kind = CellKind.List;
            c.Items = ["総務部", "営業部"];
        });
        sheet.Range("B4:C10").Configure(c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
            c.Minimum = 0;
        });
        sheet.Range("D:D").Configure(c => c.Style.HorizontalAlignment = HorizontalAlignment.Right);
        sheet.Range("D4:D10").Configure(c => c.ReadOnly = true);

        sheet["A1"].Value = "申請日";
        sheet["B1"].Value = new DateOnly(2026, 11, 1);
        var date = sheet["B1"].GetValue<DateOnly?>();
        sheet.Range("B4").SetValues([[3m, 120m], [5m, 80m]]);
        var values = sheet.Range("B4:C10").GetValues();
        var text = sheet["C4"].Text;

        sheet.InsertRows(index: 5, count: 1);
        sheet.InsertRows(index: 5, count: 1, inheritSettings: false);
        sheet.AddColumns(count: 2);

        var events = new List<SheetChangedEventArgs>();
        sheet.Changed += (_, e) => events.Add(e);
        using (sheet.BeginUpdate())
        {
            sheet.AddRows(3);
            sheet.Range("A11:F13").ClearContents();
        }

        Assert.Equal(new DateOnly(2026, 11, 1), date);
        Assert.Equal("2026/11/01", sheet["B1"].Text);
        Assert.Equal(7, values.Length);
        Assert.Equal(120m, values[0][1]);
        Assert.Equal("120", text);
        Assert.Equal("項目", sheet.Columns[0].HeaderText);
        Assert.Equal(15, sheet.RowCount);
        Assert.Equal(6, sheet.ColumnCount);

        Assert.Equal(CellKind.Label, sheet.GetSettings(6, 0).Kind);
        Assert.True(sheet.GetSettings(6, 3).ReadOnly);
        Assert.Equal(CellKind.Text, sheet.GetSettings(5, 0).Kind);
        Assert.False(sheet.GetSettings(5, 3).ReadOnly);

        Assert.Equal(HorizontalAlignment.Right, sheet.GetSettings(5, 3).Style.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Right, sheet.GetSettings(5, 4).Style.HorizontalAlignment);

        var change = Assert.Single(events);
        Assert.Equal(SheetChangeKind.Reset, change.Kind);
    }
}
