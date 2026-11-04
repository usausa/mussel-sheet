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

        string[] names = ["rows", "columns", "headerHeight", "rowHeaderWidth", "columnWidths", "defaultRowHeight", "customRowHeights", "rules", "patterns", "rowPatterns", "messages", "merges", "calendar", "reading", "eras"];
        Assert.Equal(names, map.EnumerateObject().Select(static x => x.Name));

        var rule = map.GetProperty("rules").EnumerateArray().First(static x => x.GetProperty("kind").GetString() == "list");
        Assert.Equal("Sales", rule.GetProperty("items")[0].GetProperty("text").GetString());
        Assert.True(rule.GetProperty("font").GetProperty("bold").GetBoolean());

        string[] reading = ["decimal", "group", "order", "editDate", "japanese"];
        Assert.Equal(reading, map.GetProperty("reading").EnumerateObject().Select(static x => x.Name));

        string[] calendar = ["dayNames", "firstDay", "yearMonth", "monthNames"];
        Assert.Equal(calendar, map.GetProperty("calendar").EnumerateObject().Select(static x => x.Name));
    }

    [Theory]
    [InlineData(CellCharacterRestriction.Fullwidth, "fullwidth")]
    [InlineData(CellCharacterRestriction.Halfwidth, "halfwidth")]
    [InlineData(CellCharacterRestriction.Hiragana, "hiragana")]
    [InlineData(CellCharacterRestriction.Katakana, "katakana")]
    [InlineData(CellCharacterRestriction.HalfwidthKatakana, "halfwidthKatakana")]
    [InlineData(CellCharacterRestriction.Alphanumeric, "alphanumeric")]
    [InlineData(CellCharacterRestriction.Digits, "digits")]
    public void RestrictionsReachTheScriptInItsOwnWords(CellCharacterRestriction restriction, string expected)
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c => c.CharacterRestriction = restriction);

        var map = SheetScriptJson.ToElement(SheetMapBuilder.Build(sheet, 24, 40, SheetValidationMode.Stay));

        Assert.Contains(map.GetProperty("rules").EnumerateArray(), x => x.GetProperty("restriction").GetString() == expected);
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
            new ScriptKeysData(new Dictionary<string, string?> { ["F4"] = "edit" }, new Dictionary<string, string?>()),
            new ScriptMenuData(["cut"], [new ScriptMenuItemData("log", "Log")]));

        var json = SheetScriptJson.ToElement(options);

        string[] names = ["enterDirection", "readyEnter", "tabReturn", "rowEnd", "skipReadOnly", "lastRowEnter", "commitMode", "autosize", "customEditor", "allowInsertRows", "allowDeleteRows", "allowInsertColumns", "allowDeleteColumns", "allowFill", "frozenRows", "frozenColumns", "keys", "menu"];
        Assert.Equal(names, json.EnumerateObject().Select(static x => x.Name));
        Assert.Equal("edit", json.GetProperty("keys").GetProperty("ready").GetProperty("F4").GetString());
        Assert.Equal("cut", json.GetProperty("menu").GetProperty("hidden")[0].GetString());
        Assert.Equal("Log", json.GetProperty("menu").GetProperty("items")[0].GetProperty("text").GetString());
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
