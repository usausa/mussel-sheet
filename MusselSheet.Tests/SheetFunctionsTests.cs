namespace MusselSheet;

public sealed class SheetFunctionsTests
{
    [Fact]
    public void EveryKindOfFunctionIsSavedByItsName()
    {
        var functions = new SheetFunctions();
        functions.AddCorrector("upper", static x => (x.Value as string)?.ToUpperInvariant());
        functions.AddStyleProvider("bold", static _ => new CellStyle { Bold = true });
        functions.AddSuggestionProvider("none", static _ => Task.FromResult<IReadOnlyList<string>>([]));

        var sheet = new Sheet(1, 3);
        sheet["A1"].Configure(c => c.Corrector = functions.Corrector("upper"));
        sheet["B1"].Configure(c => c.StyleProvider = functions.StyleProvider("bold"));
        sheet["C1"].Configure(c =>
        {
            c.Kind = CellKind.AutoComplete;
            c.SuggestionProvider = functions.SuggestionProvider("none");
        });

        var copy = Sheet.FromDesign(SheetDesignJson.Read(SheetDesignJson.Write(sheet.ToDesign(functions))), functions);

        Assert.Same(functions.Corrector("upper"), copy.GetSettings(0, 0).Corrector);
        Assert.Same(functions.StyleProvider("bold"), copy.GetSettings(0, 1).StyleProvider);
        Assert.Same(functions.SuggestionProvider("none"), copy.GetSettings(0, 2).SuggestionProvider);
    }

    [Fact]
    public void AddingANameAgainReplacesTheFunction()
    {
        var functions = new SheetFunctions();
        Func<CellFormatContext, string?> first = static _ => "1";
        Func<CellFormatContext, string?> second = static _ => "2";

        functions.AddFormatter("x", first);
        functions.AddFormatter("x", second);

        Assert.Same(second, functions.Formatter("x"));
        Assert.Null(functions.NameOf(first));
        Assert.Equal("x", functions.NameOf(second));
    }

    [Fact]
    public void AFunctionOfAnotherKindIsNotFound()
    {
        var functions = new SheetFunctions();
        functions.AddFormatter("x", static _ => null);

        Assert.Throws<KeyNotFoundException>(() => functions.Corrector("x"));
        Assert.Throws<KeyNotFoundException>(() => functions.Corrector("y"));
    }
}
