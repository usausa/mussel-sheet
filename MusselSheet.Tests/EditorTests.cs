namespace MusselSheet;

using Bunit;

using Microsoft.AspNetCore.Components;

public sealed class EditorTests : BunitContext
{
    public EditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void CheckCellsRenderABoxOrTheConfiguredText()
    {
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.All, c => c.Kind = CellKind.Checkbox);
        sheet["B1"].Configure(c =>
        {
            c.CheckedText = "済";
            c.UncheckedText = "未";
        });
        sheet.SetValue(0, 0, true);

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var cells = cut.FindAll(".mussel-sheet-cell");
        var box = cells[0].QuerySelector("span");
        var text = cells[1].QuerySelector("span");

        Assert.NotNull(box);
        Assert.Contains("mussel-sheet-cb", box.ClassList);
        Assert.Contains("mussel-sheet-on", box.ClassList);

        // The text inside the checkbox is for screen readers (hidden by CSS);
        // what gets copied is the edit text in data-edit
        Assert.Equal("☑", cells[0].TextContent);
        Assert.Equal("true", cells[0].GetAttribute("data-edit"));
        Assert.NotNull(text);
        Assert.Contains("mussel-sheet-ct", text.ClassList);
        Assert.DoesNotContain("mussel-sheet-on", text.ClassList);
        Assert.Equal("未", text.TextContent);
        Assert.Equal(string.Empty, cells[1].GetAttribute("data-edit"));
    }

    [Fact]
    public void CustomCellsUseTheDisplayTemplate()
    {
        var sheet = new Sheet(1, 2);
        sheet.Configure(CellRange.All, c =>
        {
            c.Kind = CellKind.Custom;
            c.Editor = "color";
        });
        sheet.SetValues(0, 0, [["#ff0000", "#0000ff"]]);

        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CustomDisplay, Display));

        string[] expected = ["color:#ff0000", "color:#0000ff"];
        Assert.Equal(expected, cut.FindAll(".mussel-sheet-cell b").Select(x => x.TextContent));

        string[] edit = ["#ff0000", "#0000ff"];
        Assert.Equal(edit, cut.FindAll(".mussel-sheet-cell").Select(x => x.GetAttribute("data-edit")));

        static RenderFragment Display(CellDisplayContext context) => builder =>
        {
            builder.OpenElement(0, "b");
            builder.AddContent(1, $"{context.Name}:{context.Text}");
            builder.CloseElement();
        };
    }

    [Fact]
    public async Task CustomEditorCommitsTheValue()
    {
        var sheet = CustomSheet();
        CellEditContext? opened = null;
        var committed = new List<CellCommittedEventArgs>();
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CustomEditor, Editor).Add(x => x.CellCommitted, committed.Add));

        var ok = await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 0)).ConfigureAwait(true);

        Assert.True(ok);
        Assert.Equal("color", cut.Find(".mussel-sheet-custom .editor").TextContent);
        Assert.NotNull(opened);
        Assert.Equal(new CellAddress(0, 0), opened.Address);

        opened.Value = "#00ff00";
        var result = await cut.InvokeAsync(() => cut.Instance.CommitCustomEditorAsync()).ConfigureAwait(true);

        Assert.True(result.Closed);
        Assert.Null(result.Error);
        Assert.Equal("#00ff00", sheet.GetValue(0, 0));
        Assert.Null(sheet.GetInputText(0, 0));
        Assert.Empty(cut.FindAll(".mussel-sheet-custom"));
        Assert.Equal("#00ff00", Assert.Single(committed).Value);
        Assert.True(cut.Instance.CanUndo);

        RenderFragment Editor(CellEditContext context) => builder =>
        {
            opened = context;
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "editor");
            builder.AddContent(2, context.Name);
            builder.CloseElement();
        };
    }

    [Fact]
    public async Task CustomEditorStaysOpenOnValidationErrors()
    {
        var sheet = CustomSheet();
        sheet["A1"].Configure(c => c.Required = true);
        CellEditContext? opened = null;
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CustomEditor, Editor));
        await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 0)).ConfigureAwait(true);

        var result = await cut.InvokeAsync(() => cut.Instance.CommitCustomEditorAsync()).ConfigureAwait(true);

        Assert.False(result.Closed);
        Assert.Equal(SheetTexts.Japanese.Required, result.Error);
        Assert.NotNull(opened);
        Assert.Equal(SheetTexts.Japanese.Required, opened.Error);
        Assert.Equal(SheetTexts.Japanese.Required, cut.Find(".mussel-sheet-custom").TextContent);

        var ok = await cut.InvokeAsync(() => opened.CommitAsync("OK")).ConfigureAwait(true);

        Assert.True(ok);
        Assert.Equal("OK", sheet.GetValue(0, 0));
        Assert.Empty(cut.FindAll(".mussel-sheet-custom"));

        RenderFragment Editor(CellEditContext context) => builder =>
        {
            opened = context;
            builder.AddContent(0, context.Error);
        };
    }

    [Fact]
    public async Task CustomEditorNeedsATemplateAndAnEditableCustomCell()
    {
        var sheet = new Sheet(1, 3);
        sheet["A1"].Configure(c => c.Kind = CellKind.Custom);
        sheet["B1"].Configure(c =>
        {
            c.Kind = CellKind.Custom;
            c.ReadOnly = true;
        });
        var plain = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CustomEditor, Editor));

        Assert.False(await plain.InvokeAsync(() => plain.Instance.OpenCustomEditor(0, 0)).ConfigureAwait(true));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 1)).ConfigureAwait(true));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 2)).ConfigureAwait(true));
        Assert.True(await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 0)).ConfigureAwait(true));

        await cut.InvokeAsync(cut.Instance.CancelCustomEditor).ConfigureAwait(true);

        Assert.Empty(cut.FindAll(".mussel-sheet-custom"));
        Assert.Null(sheet.GetValue(0, 0));

        static RenderFragment Editor(CellEditContext context) => builder => builder.AddContent(0, context.Name);
    }

    [Fact]
    public async Task SuggestionsComeFromTheProvider()
    {
        var sheet = new Sheet(1, 2);
        string? asked = null;
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.AutoComplete;
            c.SuggestionProvider = context =>
            {
                asked = context.Text;
                return Task.FromResult<IReadOnlyList<string>>([.. Enumerable.Range(1, 60).Select(x => $"{context.Text}{x}")]);
            };
        });
        sheet["B1"].Configure(c => c.Kind = CellKind.AutoComplete);
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var items = await cut.InvokeAsync(() => cut.Instance.GetSuggestionsAsync(0, 0, "東")).ConfigureAwait(true);

        Assert.Equal("東", asked);
        Assert.NotNull(items);
        Assert.Equal(50, items.Count);
        Assert.Equal("東1", items[0]);
        Assert.Null(await cut.InvokeAsync(() => cut.Instance.GetSuggestionsAsync(0, 1, "東")).ConfigureAwait(true));
    }

    [Fact]
    public async Task NewerSuggestionRequestsCancelOlderOnes()
    {
        var sheet = new Sheet(1, 1);
        var first = new TaskCompletionSource<IReadOnlyList<string>>();
        var firstToken = CancellationToken.None;
        var calls = 0;
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.AutoComplete;
            c.SuggestionProvider = context =>
            {
                if (calls++ == 0)
                {
                    firstToken = context.CancellationToken;
                    return first.Task;
                }

                return Task.FromResult<IReadOnlyList<string>>(["新しい"]);
            };
        });
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var older = cut.InvokeAsync(() => cut.Instance.GetSuggestionsAsync(0, 0, "あ"));
        var newer = await cut.InvokeAsync(() => cut.Instance.GetSuggestionsAsync(0, 0, "あい")).ConfigureAwait(true);
        first.SetResult(["古い"]);

        Assert.True(firstToken.IsCancellationRequested);
        Assert.Null(await older.ConfigureAwait(true));
        string[] expected = ["新しい"];
        Assert.Equal(expected, newer);
    }

    [Fact]
    public async Task MaskAndConversionsShapeTheStoredText()
    {
        var sheet = new Sheet(1, 2);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Mask;
            c.Mask = "999-9999";
        });
        sheet["B1"].Configure(c =>
        {
            c.CharacterConversions = CellCharacterConversions.HiraganaToKatakana;
            c.CharacterRestriction = CellCharacterRestriction.Katakana;
        });
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet));

        var zip = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 0, "１２３４５６７", true)).ConfigureAwait(true);
        var kana = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 1, "やまだ", true)).ConfigureAwait(true);
        var wrong = await cut.InvokeAsync(() => cut.Instance.CommitAsync(0, 1, "山田", false)).ConfigureAwait(true);

        Assert.True(zip.Ok);
        Assert.Equal("123-4567", sheet.GetValue(0, 0));
        Assert.Equal("123-4567", sheet.GetInputText(0, 0));
        Assert.True(kana.Ok);
        Assert.Equal("ヤマダ", sheet.GetValue(0, 1));
        Assert.Equal(SheetTexts.Japanese.Katakana, wrong.Error);
        Assert.Equal("ヤマダ", sheet.GetValue(0, 1));
    }

    private static Sheet CustomSheet()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(c =>
        {
            c.Kind = CellKind.Custom;
            c.Editor = "color";
        });
        return sheet;
    }
}
