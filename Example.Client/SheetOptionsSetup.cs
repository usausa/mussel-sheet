namespace Example.Client;

// What applies to every sheet of the app. Example and Example.Client register the same options, for Server and WebAssembly
public static class SheetOptionsSetup
{
    public static void Configure(MusselSheetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ReadyKeys["F4"] = SheetCommands.Edit;

        options.Menu.Items.Add(new SheetMenuItem { Name = "log-selection", Text = "Log the selection", Texts = { ["ja"] = "選択範囲を記録に出す" } });

        options.Cultures[string.Empty] = new SheetCulture { Texts = SheetTexts.English with { Required = "This cell is required." } };
    }
}
