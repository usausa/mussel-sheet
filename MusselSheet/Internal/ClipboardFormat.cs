namespace MusselSheet.Internal;

using System.Text;

// Must stay in step with parseClipboard, toTsv and toHtml in MusselSheet.js: change both together
internal static class ClipboardFormat
{
    // A <style> class like Excel's own copy keeps leading zeros; mso-data-placement keeps <br> in the cell
    private const string ExcelStyle = "<style>.istext{mso-number-format:\"\\@\";}br{mso-data-placement:same-cell;}</style>";

    private const string TextCell = "<td class=\"istext\" style='mso-number-format:\"\\@\"'>";

    // Drops one trailing line break, as Excel writes one; a cell without a closing quote is read unquoted
    public static List<string[]> Parse(string? text)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(text))
        {
            return rows;
        }

        text = TrimLastNewline(text);
        var row = new List<string>();
        var index = 0;
        while (true)
        {
            var (cell, next) = ReadCell(text, index);
            row.Add(cell);
            if (next >= text.Length)
            {
                rows.Add([.. row]);
                return rows;
            }

            if (text[next] == '\t')
            {
                index = next + 1;
                continue;
            }

            rows.Add([.. row]);
            row.Clear();
            index = next + ((text[next] == '\r') && (next + 1 < text.Length) && (text[next + 1] == '\n') ? 2 : 1);
        }
    }

    public static string ToTsv(IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                if (c > 0)
                {
                    builder.Append('\t');
                }

                builder.Append(Quote(row[c]));
            }

            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    public static string ToHtml(IReadOnlyList<IReadOnlyList<string>> rows, Func<int, int, bool> isText)
    {
        var builder = new StringBuilder(ExcelStyle).Append("<table>");
        for (var r = 0; r < rows.Count; r++)
        {
            builder.Append("<tr>");
            for (var c = 0; c < rows[r].Count; c++)
            {
                builder.Append(isText(r, c) ? TextCell : "<td>").Append(BreakLines(Escape(rows[r][c]))).Append("</td>");
            }

            builder.Append("</tr>");
        }

        return builder.Append("</table>").ToString();
    }

    private static string BreakLines(string text) =>
        text.Replace("\r\n", "<br>", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal).Replace("\r", "<br>", StringComparison.Ordinal);

    private static string TrimLastNewline(string text)
    {
        if (text.EndsWith("\r\n", StringComparison.Ordinal))
        {
            return text[..^2];
        }

        return (text.EndsWith('\n') || text.EndsWith('\r')) ? text[..^1] : text;
    }

    private static (string Cell, int Next) ReadCell(string text, int index)
    {
        if ((index < text.Length) && (text[index] == '"') && (ReadQuoted(text, index) is { } quoted))
        {
            return quoted;
        }

        var end = text.IndexOfAny(['\t', '\r', '\n'], index);
        return end < 0 ? (text[index..], text.Length) : (text[index..end], end);
    }

    private static (string Cell, int Next)? ReadQuoted(string text, int index)
    {
        var builder = new StringBuilder();
        for (var i = index + 1; i < text.Length; i++)
        {
            if (text[i] != '"')
            {
                builder.Append(text[i]);
                continue;
            }

            if ((i + 1 < text.Length) && (text[i + 1] == '"'))
            {
                builder.Append('"');
                i++;
                continue;
            }

            var next = i + 1;
            return (next >= text.Length) || (text[next] is '\t' or '\r' or '\n') ? (builder.ToString(), next) : null;
        }

        return null;
    }

    // The same five characters as escapeHtml in MusselSheet.js
    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);

    private static string Quote(string cell) =>
        (cell.IndexOfAny(['\t', '\r', '\n']) >= 0) || cell.StartsWith('"')
            ? "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : cell;
}
