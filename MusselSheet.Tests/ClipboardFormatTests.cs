namespace MusselSheet;

using MusselSheet.Internal;

public sealed class ClipboardFormatTests
{
    public static TheoryData<string, string[][]> Tables => new()
    {
        { "a\tb\r\nc\td\r\n", [["a", "b"], ["c", "d"]] },
        { "営業部\r\n", [["営業部"]] },
        { "\"1行目\n2行目\"\tb\r\n", [["1行目\n2行目", "b"]] },
        { "\"引用符\"\"あり\"", [["引用符\"あり"]] },
        { "\"閉じていない", [["\"閉じていない"]] },
        { "\"途中\"の引用符\tb", [["\"途中\"の引用符", "b"]] },
        { "\r\n", [[string.Empty]] },
        { "a\t\tc", [["a", string.Empty, "c"]] },
        { "a\n\nb", [["a"], [string.Empty], ["b"]] },
        { "a\rb", [["a"], ["b"]] }
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void ParseFollowsExcelTsv(string text, string[][] expected)
    {
        Assert.Equal(expected, ClipboardFormat.Parse(text));
    }

    [Fact]
    public void EmptyTextHasNoRows()
    {
        Assert.Empty(ClipboardFormat.Parse(string.Empty));
        Assert.Empty(ClipboardFormat.Parse(null));
    }

    [Fact]
    public void TsvQuotesOnlyWhatNeedsQuoting()
    {
        string[][] rows = [["a", "b"], ["c\td", "\"x"], ["中\"の引用符", string.Empty]];

        var tsv = ClipboardFormat.ToTsv(rows);

        Assert.Equal("a\tb\r\n\"c\td\"\t\"\"\"x\"\r\n中\"の引用符\t\r\n", tsv);
        Assert.Equal(rows, ClipboardFormat.Parse(tsv));
    }

    [Fact]
    public void HtmlEscapesTextAndMarksTextCells()
    {
        string[][] rows = [["<a>&'\"", "0123"]];

        var html = ClipboardFormat.ToHtml(rows, (_, c) => c == 1);

        // The text format is also a <style> class, as Excel writes it; a style attribute alone lost leading zeros
        Assert.Equal("<style>.istext{mso-number-format:\"\\@\";}br{mso-data-placement:same-cell;}</style><table><tr><td>&lt;a&gt;&amp;&#39;&quot;</td><td class=\"istext\" style='mso-number-format:\"\\@\"'>0123</td></tr></table>", html);
    }

    [Fact]
    public void HtmlKeepsLineBreaksInTheCell()
    {
        string[][] rows = [["1行目\n2行目"]];

        Assert.EndsWith("<table><tr><td>1行目<br>2行目</td></tr></table>", ClipboardFormat.ToHtml(rows, (_, _) => false), StringComparison.Ordinal);
    }
}
