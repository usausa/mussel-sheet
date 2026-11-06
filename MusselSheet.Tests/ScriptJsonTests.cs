namespace MusselSheet;

using System.Text.Json;

using MusselSheet.Internal;

public sealed class ScriptJsonTests
{
    [Fact]
    public void MapUsesTheNamesTheScriptReads()
    {
        var sheet = new Sheet(2, 2);
        sheet.Range("A1").Configure(static c =>
        {
            c.Kind = CellKind.List;
            c.Items = [new CellListItem("S", "Sales")];
            c.Style.Bold = true;
        });
        sheet.Range("A1:B1").Merge();

        var map = SheetScriptJson.ToElement(SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay));

        string[] names = ["rows", "columns", "headerHeight", "rowHeaderWidth", "columnWidths", "defaultRowHeight", "customRowHeights", "rules", "patterns", "rowPatterns", "messages", "merges", "calendar", "reading"];
        Assert.Equal(names, map.EnumerateObject().Select(static x => x.Name));

        var rule = map.GetProperty("rules").EnumerateArray().First(static x => x.GetProperty("kind").GetString() == "list");
        Assert.Equal("Sales", rule.GetProperty("items")[0].GetProperty("text").GetString());
        Assert.True(rule.GetProperty("font").GetProperty("bold").GetBoolean());

        string[] reading = ["decimal", "group", "time", "order", "editDate", "forms", "readings", "dateReadings", "eras", "eraFirstYear", "eraFirstYearUnit", "sets"];
        Assert.Equal(reading, map.GetProperty("reading").EnumerateObject().Select(static x => x.Name));

        string[] calendar = ["dayNames", "firstDay", "yearMonth", "monthNames"];
        Assert.Equal(calendar, map.GetProperty("calendar").EnumerateObject().Select(static x => x.Name));
    }

    [Theory]
    [InlineData("fullwidth")]
    [InlineData("halfwidth")]
    [InlineData("hiragana")]
    [InlineData("katakana")]
    [InlineData("halfwidthKatakana")]
    [InlineData("alphanumeric")]
    [InlineData("digits")]
    public void RestrictionsReachTheScriptAsData(string name)
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.CharacterRestriction = TestLanguages.Set(name));

        var map = SheetScriptJson.ToElement(SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay));

        Assert.Contains(map.GetProperty("rules").EnumerateArray(), x => x.GetProperty("restriction").GetString() == name);
        Assert.True(map.GetProperty("reading").GetProperty("sets").GetProperty(name).GetArrayLength() >= 2);
    }

    [Fact]
    public void MessagesKeepTheirKeys()
    {
        var map = SheetScriptJson.ToElement(SheetMapBuilder.Build(new Sheet(1, 1), 24, 40, SheetValidationMode.Stay));

        Assert.Equal(JsonValueKind.String, map.GetProperty("messages").GetProperty("required").ValueKind);
    }

    [Fact]
    public void OptionsUseTheNamesTheScriptReads()
    {
        var options = new ScriptOptionsData(
            "down",
            "move",
            true,
            "next",
            "enterTab",
            "stop",
            "optimistic",
            true,
            false,
            false,
            false,
            false,
            false,
            true,
            1,
            2,
            new Dictionary<string, string?> { ["F4"] = "edit" },
            new Dictionary<string, string?>(),
            ["cut"],
            [new ScriptItem("log", "Log")]);

        var json = SheetScriptJson.ToElement(options);

        string[] names = ["enterDirection", "readyEnter", "tabReturn", "rowEnd", "skipReadOnly", "lastRowEnter", "commitMode", "autosize", "customEditor", "allowInsertRows", "allowDeleteRows", "allowInsertColumns", "allowDeleteColumns", "allowFill", "frozenRows", "frozenColumns", "readyKeys", "editingKeys", "hiddenMenuItems", "menuItems"];
        Assert.Equal(names, json.EnumerateObject().Select(static x => x.Name));
        Assert.Equal("edit", json.GetProperty("readyKeys").GetProperty("F4").GetString());
        Assert.Equal("cut", json.GetProperty("hiddenMenuItems")[0].GetString());
        Assert.Equal("log", json.GetProperty("menuItems")[0].GetProperty("value").GetString());
        Assert.Equal("Log", json.GetProperty("menuItems")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void ResultsUseTheNamesTheScriptReads()
    {
        Assert.False(SheetScriptJson.ToElement(new CommitResult(false, "e", true)).GetProperty("ok").GetBoolean());
        Assert.Equal("e", SheetScriptJson.ToElement(new CommitResult(false, "e", true)).GetProperty("error").GetString());
        Assert.True(SheetScriptJson.ToElement(new CommitResult(false, "e", true)).GetProperty("stored").GetBoolean());
        Assert.True(SheetScriptJson.ToElement(new CustomEditorResult(true, null)).GetProperty("closed").GetBoolean());
        Assert.Equal(4, SheetScriptJson.ToElement(new PasteResult(1, 2, 3, 4, "m")).GetProperty("right").GetInt32());
        Assert.Equal("m", SheetScriptJson.ToElement(new PasteResult(1, 2, 3, 4, "m")).GetProperty("message").GetString());
        Assert.Equal("h", SheetScriptJson.ToElement(new ClipboardText("t", "h")).GetProperty("html").GetString());
    }
}
