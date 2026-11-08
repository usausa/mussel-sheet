namespace Example.Client;

// Example and Example.Client register these, so Server and WebAssembly use the same options
public static class SheetOptionsSetup
{
    public static void Configure(MusselSheetOptions options)
    {
        options.ReadyKeys["F4"] = SheetCommands.Edit;

        options.Menu.Items.Add(new SheetMenuItem { Name = "log-selection", Text = "Log the selection", Texts = { ["ja"] = "選択範囲を記録に出す" } });

        options.Cultures[string.Empty] = new SheetCulture { Texts = SheetTexts.English with { Required = "This cell is required." } };
    }
}
