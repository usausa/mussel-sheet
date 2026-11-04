namespace MusselSheet;

public sealed class CellAccessTests
{
    [Fact]
    public void CellAnswersForItsOwnAddress()
    {
        var sheet = new Sheet(1, 1);
        sheet.Range("A1").Configure(static c =>
        {
            c.Kind = CellKind.Number;
            c.Format = "N0";
        });
        var cell = sheet["A1"];
        cell.Value = 1234m;

        Assert.Equal("1,234", cell.Text);
        Assert.Equal("1234", cell.EditText);
        Assert.Null(cell.InputText);
        Assert.True(cell.TryGetValue<int>(out var number));
        Assert.Equal(1234, number);

        cell.ClearContents();
        Assert.Null(cell.Value);
    }

    [Fact]
    public void CellsCoversTheWholeSheet()
    {
        var sheet = new Sheet(2, 2);

        sheet.Cells.Configure(static c => c.ReadOnly = true);

        Assert.True(sheet.GetSettings(0, 0).ReadOnly);
        Assert.True(sheet.GetSettings(1, 1).ReadOnly);
    }

    [Fact]
    public void RangeIsWrittenAsItsAddress()
    {
        Assert.Equal("B2:C3", new Sheet(3, 3).Range("B2:C3").ToString());
    }

    [Fact]
    public void ComputationsWorkOnWholeColumns()
    {
        var sheet = new Sheet(3, 3);
        sheet.ComputeRows("B:B", ["A"], static x => x.Get<decimal?>("A") * 2);
        sheet.Compute("C1", ["A:A"], static x => x.Values("A:A").OfType<decimal>().Sum());

        sheet.SetValue(1, 0, 3m);
        Assert.Equal(6m, sheet.GetValue(1, 1));
        Assert.Equal(3m, sheet.GetValue(0, 2));

        sheet.AddRows(1);
        sheet.SetValue(3, 0, 4m);
        Assert.Equal(8m, sheet.GetValue(3, 1));
        Assert.Equal(7m, sheet.GetValue(0, 2));
    }
}
