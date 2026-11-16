namespace MusselSheet;

public sealed class ChangeVersionTests
{
    [Fact]
    public void ValueChangesOnlyTheRowVersion()
    {
        var sheet = new Sheet(2, 2);
        var structure = sheet.StructureVersion;
        var row0 = sheet.Rows[0].ContentVersion;
        var row1 = sheet.Rows[1].ContentVersion;

        sheet.SetValue(1, 0, 1m);
        sheet.SetError(1, 1, "エラー");

        Assert.Equal(structure, sheet.StructureVersion);
        Assert.Equal(row0, sheet.Rows[0].ContentVersion);
        Assert.True(sheet.Rows[1].ContentVersion > row1);
    }

    [Fact]
    public void SettingsLayoutAndRowsChangeTheStructureVersion()
    {
        var sheet = new Sheet(2, 2);
        var before = sheet.StructureVersion;

        sheet.Configure(CellRange.All, c => c.ReadOnly = true);
        var afterSettings = sheet.StructureVersion;
        sheet.Columns[0].Width = 50;
        var afterLayout = sheet.StructureVersion;
        sheet.AddRows(1);

        Assert.True(afterSettings > before);
        Assert.True(afterLayout > afterSettings);
        Assert.True(sheet.StructureVersion > afterLayout);
    }

    [Fact]
    public void VersionsChangeInsideBeginUpdate()
    {
        var sheet = new Sheet(1, 1);
        var structure = sheet.StructureVersion;
        var content = sheet.Rows[0].ContentVersion;
        using var update = sheet.BeginUpdate();

        sheet.Configure(CellRange.All, c => c.ReadOnly = true);
        sheet.SetValue(0, 0, 1m);

        Assert.True(sheet.StructureVersion > structure);
        Assert.True(sheet.Rows[0].ContentVersion > content);
    }
}
