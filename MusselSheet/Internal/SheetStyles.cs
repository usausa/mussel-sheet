namespace MusselSheet.Internal;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

// Fixed kinds use classes in MusselSheet.css; colors, sizes, indents and widths go in a per-instance <style>
internal sealed class SheetStyles
{
    private readonly Dictionary<string, int> colors = new(StringComparer.Ordinal);

    private readonly Dictionary<(ResolvedCellSettings Settings, CellEdges Edges, CellBorder? Left, CellBorder? Top), string> cellClasses = new(CellKeyComparer.Instance);

    private readonly Dictionary<CellRange, string> mergeClasses = [];

    private string baseCss = string.Empty;

    private string restCss = string.Empty;

    public SheetStyles(string scope)
    {
        Scope = scope;
    }

    public string Scope { get; }

    public string Css { get; private set; } = string.Empty;

    // Colors are collected from the rules up front, so their classes exist before any cell renders
    public void Update(Sheet sheet, int rowHeaderWidth, int frozenColumns)
    {
        colors.Clear();
        cellClasses.Clear();
        var fontSizes = new SortedSet<int>();
        var indents = new SortedSet<int>();
        foreach (var rule in sheet.Rules)
        {
            var style = rule.Settings.Style;
            if (style.FontSize is { } size)
            {
                fontSizes.Add(size);
            }

            if (style.IndentLevel is > 0 and var level)
            {
                indents.Add(level);
            }

            Register(style.FontColor);
            Register(style.FillColor);
            Register(style.Borders.Top?.Color);
            Register(style.Borders.Right?.Color);
            Register(style.Borders.Bottom?.Color);
            Register(style.Borders.Left?.Color);
        }

        var widths = sheet.Columns.Select(x => x.ActualWidth).ToArray();
        var frozen = Math.Clamp(frozenColumns, 0, widths.Length);
        var header = rowHeaderWidth > 0 ? [rowHeaderWidth] : Array.Empty<int>();
        var css = new StringBuilder();
        AppendTemplate(css, $".{Scope} .mussel-sheet-row,.{Scope} .mussel-sheet-colhead", [.. header, .. widths[..frozen], widths[frozen..].Sum()]);

        // The frozen boundary is a box-shadow on the last frozen column, repeated in the error rule that overrides it
        var left = rowHeaderWidth;
        for (var c = 0; c < frozen; c++)
        {
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .mussel-sheet-row>.mussel-sheet-cell[data-c=\"{c}\"],.{Scope} .mussel-sheet-colhead-cell[data-c=\"{c}\"]{{position:sticky;left:{left}px;z-index:2}}");
            left += widths[c];
        }

        if (frozen > 0)
        {
            var last = frozen - 1;
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .mussel-sheet-row>.mussel-sheet-cell[data-c=\"{last}\"],.{Scope} .mussel-sheet-colhead-cell[data-c=\"{last}\"]{{box-shadow:1px 0 0 var(--mussel-sheet-freeze-color)}}");
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .mussel-sheet-row>.mussel-sheet-cell.mussel-sheet-error[data-c=\"{last}\"]{{box-shadow:1px 0 0 var(--mussel-sheet-freeze-color),inset 0 0 0 2px var(--mussel-sheet-error-color)}}");
        }

        mergeClasses.Clear();
        foreach (var range in sheet.MergeAreas)
        {
            var name = String.Create(CultureInfo.InvariantCulture, $"m{mergeClasses.Count}");
            mergeClasses[range] = name;
            var width = widths[range.Left..(range.Right + 1)].Sum();
            var height = Enumerable.Range(range.Top, range.Bottom - range.Top + 1).Sum(r => sheet.Rows[r].ActualHeight);
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .{name}{{--mussel-sheet-merge-w:{width}px;--mussel-sheet-merge-h:{height}px}}");
        }

        foreach (var size in fontSizes)
        {
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .fs{size}{{font-size:{size}px}}");
        }

        foreach (var level in indents)
        {
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .il{level}{{padding-left:calc(4px + {level}em)}}.{Scope} .ir{level}{{padding-right:calc(4px + {level}em)}}");
        }

        foreach (var (color, id) in colors)
        {
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .fg{id}{{color:{color}}}.{Scope} .bg{id}{{background-color:{color}}}");
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .brc{id}{{border-right-color:{color}}}.{Scope} .bbc{id}{{border-bottom-color:{color}}}");
            css.Append(CultureInfo.InvariantCulture, $".{Scope} .blc{id}{{border-left-color:{color}}}.{Scope} .btc{id}{{border-top-color:{color}}}");
        }

        baseCss = css.ToString();
        Css = baseCss + restCss;
    }

    public void UpdateColumns(Sheet sheet, int frozenColumns, int first, int last)
    {
        var widths = sheet.Columns.Select(x => x.ActualWidth).ToArray();
        var frozen = Math.Clamp(frozenColumns, 0, widths.Length);
        var start = Math.Clamp(first, frozen, widths.Length);
        var end = Math.Clamp(last + 1, start, widths.Length);
        var css = new StringBuilder();
        css.Append(CultureInfo.InvariantCulture, $".{Scope} .mussel-sheet-rest{{padding-left:{widths[frozen..start].Sum()}px}}");
        AppendTemplate(css, $".{Scope} .mussel-sheet-rest", widths[start..end]);
        restCss = css.ToString();
        Css = baseCss + restCss;
    }

    public string GetMergeClass(CellRange range) => mergeClasses.TryGetValue(range, out var name) ? name : string.Empty;

    // Shared edges are right and bottom lines; left and top lines are drawn only in the first column and row
    public string GetCellClass(ResolvedCellSettings settings, CellEdges edges, CellBorder? left, CellBorder? top)
    {
        var key = (settings, edges, left, top);
        if (cellClasses.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var style = settings.Style;
        var builder = new StringBuilder("mussel-sheet-cell");
        var alignment = EffectiveAlignment(settings);
        builder.Append(alignment switch
        {
            HorizontalAlignment.Center => " mussel-sheet-ha-c",
            HorizontalAlignment.Right => " mussel-sheet-ha-r",
            _ => string.Empty
        });
        builder.Append(style.VerticalAlignment switch
        {
            VerticalAlignment.Top => " mussel-sheet-va-t",
            VerticalAlignment.Bottom => " mussel-sheet-va-b",
            _ => string.Empty
        });
        if (style.Bold)
        {
            builder.Append(" mussel-sheet-b");
        }

        if (style.Italic)
        {
            builder.Append(" mussel-sheet-i");
        }

        if (style.Underline)
        {
            builder.Append(" mussel-sheet-u");
        }

        if (style.Strikethrough)
        {
            builder.Append(" mussel-sheet-s");
        }

        if (style.FontSize is { } size)
        {
            builder.Append(CultureInfo.InvariantCulture, $" fs{size}");
        }

        if ((style.IndentLevel > 0) && (alignment != HorizontalAlignment.Center))
        {
            builder.Append(CultureInfo.InvariantCulture, $" {(alignment == HorizontalAlignment.Right ? "ir" : "il")}{style.IndentLevel}");
        }

        if (settings.WrapText)
        {
            builder.Append(" mussel-sheet-ml");
        }

        if (settings.ReadOnly)
        {
            builder.Append(" mussel-sheet-ro");
        }

        AppendColor(builder, "fg", style.FontColor);
        AppendColor(builder, "bg", style.FillColor);
        AppendBorder(builder, 'r', edges.Right);
        AppendBorder(builder, 'b', edges.Bottom);
        AppendBorder(builder, 'l', left);
        AppendBorder(builder, 't', top);
        if (!String.IsNullOrWhiteSpace(style.CssClass))
        {
            builder.Append(' ').Append(style.CssClass.Trim());
        }

        var result = builder.ToString();
        cellClasses[key] = result;
        return result;
    }

    // Inline, as it varies per value; borders and vertical alignment never change by value
    public static string? GetValueStyle(ResolvedCellSettings settings, CellStyle patch)
    {
        var css = new StringBuilder();
        var style = settings.Style;
        var alignment = EffectiveAlignment(settings);
        var (leftBefore, rightBefore) = Indents(alignment, style.IndentLevel);
        if ((patch.HorizontalAlignment is { } horizontal) && (horizontal != HorizontalAlignment.General))
        {
            alignment = horizontal;
            css.Append(horizontal switch
            {
                HorizontalAlignment.Center => "text-align:center;",
                HorizontalAlignment.Right => "text-align:right;",
                _ => "text-align:left;"
            });
        }

        if (patch.Bold is { } bold)
        {
            css.Append(bold ? "font-weight:600;" : "font-weight:400;");
        }

        if (patch.Italic is { } italic)
        {
            css.Append(italic ? "font-style:italic;" : "font-style:normal;");
        }

        if ((patch.Underline is not null) || (patch.Strikethrough is not null))
        {
            var line = (patch.Underline ?? style.Underline, patch.Strikethrough ?? style.Strikethrough) switch
            {
                (true, true) => "underline line-through",
                (true, false) => "underline",
                (false, true) => "line-through",
                _ => "none"
            };
            css.Append(CultureInfo.InvariantCulture, $"text-decoration-line:{line};");
        }

        if (patch.FontSize is { } size)
        {
            css.Append(CultureInfo.InvariantCulture, $"font-size:{size}px;");
        }

        // The class indents the side of the cell's own alignment, so a patch writes only the sides whose indent it changes
        var (leftAfter, rightAfter) = Indents(alignment, patch.IndentLevel ?? style.IndentLevel);
        AppendPadding(css, "padding-left", leftBefore, leftAfter);
        AppendPadding(css, "padding-right", rightBefore, rightAfter);
        AppendInlineColor(css, "color", patch.FontColor);
        AppendInlineColor(css, "background-color", patch.FillColor);
        return css.Length == 0 ? null : css.ToString();
    }

    private static (int Left, int Right) Indents(HorizontalAlignment alignment, int? level) =>
        (level is not > 0) || (alignment == HorizontalAlignment.Center) ? (0, 0) : alignment == HorizontalAlignment.Right ? (0, level.Value) : (level.Value, 0);

    private static void AppendPadding(StringBuilder css, string side, int before, int after)
    {
        if (after == before)
        {
            return;
        }

        if (after > 0)
        {
            css.Append(CultureInfo.InvariantCulture, $"{side}:calc(4px + {after}em);");
        }
        else
        {
            css.Append(side).Append(":4px;");
        }
    }

    private static void AppendTemplate(StringBuilder css, string selector, int[] widths)
    {
        css.Append(CultureInfo.InvariantCulture, $"{selector}{{grid-template-columns:");
        if (widths.Length == 0)
        {
            css.Append(" none");
        }

        foreach (var width in widths)
        {
            css.Append(CultureInfo.InvariantCulture, $" {width}px");
        }

        css.Append('}');
    }

    private static void AppendInlineColor(StringBuilder css, string property, string? color)
    {
        if ((color?.Trim() is { } trimmed) && IsSafeColor(trimmed))
        {
            css.Append(CultureInfo.InvariantCulture, $"{property}:{trimmed};");
        }
    }

    private static HorizontalAlignment EffectiveAlignment(ResolvedCellSettings settings)
    {
        var alignment = settings.Style.HorizontalAlignment;
        if (alignment != HorizontalAlignment.General)
        {
            return alignment;
        }

        return settings.Kind switch
        {
            CellKind.Number or CellKind.Date or CellKind.Time => HorizontalAlignment.Right,
            CellKind.Checkbox => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Left
        };
    }

    // Colors are written into <style>, so only values made of characters that are safe in CSS are used
    private static bool IsSafeColor(string value)
    {
        if (value.Length is 0 or > 64)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!Char.IsAsciiLetterOrDigit(ch) && (ch is not ('#' or '(' or ')' or ',' or '.' or '%' or ' ' or '-')))
            {
                return false;
            }
        }

        return true;
    }

    private void Register(string? color)
    {
        if ((color is not null) && IsSafeColor(color))
        {
            colors.TryAdd(color, colors.Count);
        }
    }

    private void AppendColor(StringBuilder builder, string prefix, string? color)
    {
        if ((color is not null) && colors.TryGetValue(color, out var id))
        {
            builder.Append(CultureInfo.InvariantCulture, $" {prefix}{id}");
        }
    }

    private void AppendBorder(StringBuilder builder, char side, CellBorder? line)
    {
        if (line is not { } value)
        {
            return;
        }

        var name = value.Style switch
        {
            BorderLineStyle.Thin => "thin",
            BorderLineStyle.Medium => "medium",
            BorderLineStyle.Thick => "thick",
            BorderLineStyle.Dashed => "dashed",
            BorderLineStyle.Dotted => "dotted",
            BorderLineStyle.DoubleLine => "double",
            _ => null
        };
        if (name is null)
        {
            return;
        }

        builder.Append(CultureInfo.InvariantCulture, $" mussel-sheet-b{side}-{name}");
        AppendColor(builder, $"b{side}c", value.Color);
    }

    // Resolved settings are shared instances, so they are compared by reference instead of by every member
    private sealed class CellKeyComparer : IEqualityComparer<(ResolvedCellSettings Settings, CellEdges Edges, CellBorder? Left, CellBorder? Top)>
    {
        public static readonly CellKeyComparer Instance = new();

        public bool Equals((ResolvedCellSettings Settings, CellEdges Edges, CellBorder? Left, CellBorder? Top) x, (ResolvedCellSettings Settings, CellEdges Edges, CellBorder? Left, CellBorder? Top) y) =>
            ReferenceEquals(x.Settings, y.Settings) && (x.Edges == y.Edges) && (x.Left == y.Left) && (x.Top == y.Top);

        public int GetHashCode((ResolvedCellSettings Settings, CellEdges Edges, CellBorder? Left, CellBorder? Top) obj) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Settings), obj.Edges, obj.Left, obj.Top);
    }
}
