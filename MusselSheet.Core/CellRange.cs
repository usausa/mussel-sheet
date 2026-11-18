namespace MusselSheet;

using System.Globalization;

public readonly record struct CellRange
{
    public const int End = Int32.MaxValue;

    public static CellRange All { get; } = new(0, 0, End, End);

    public int Top { get; }

    public int Left { get; }

    public int Bottom { get; }

    public int Right { get; }

    public bool IsEntireColumns => (Top == 0) && (Bottom == End);

    public bool IsEntireRows => (Left == 0) && (Right == End);

    public CellRange(int top, int left, int bottom, int right)
    {
        Top = top;
        Left = left;
        Bottom = bottom;
        Right = right;
    }

    public static CellRange Cell(int row, int column) => new(row, column, row, column);

    public static CellRange Cell(CellAddress address) => Cell(address.Row, address.Column);

    public static CellRange Rows(int top, int bottom) => new(top, 0, bottom, End);

    public static CellRange Columns(int left, int right) => new(0, left, End, right);

    public static CellRange Parse(string text)
    {
        if (!TryParse(text, out var range))
        {
            throw new FormatException($"Invalid range: {text}");
        }

        return range;
    }

    public static bool TryParse(string? text, out CellRange range)
    {
        range = default;
        var span = text.AsSpan().Trim();
        if (span is "*")
        {
            range = All;
            return true;
        }

        var colon = span.IndexOf(':');
        if (colon < 0)
        {
            if (!CellAddress.TryParse(span, out var address))
            {
                return false;
            }

            range = Cell(address);
            return true;
        }

        var first = span[..colon].Trim();
        var second = span[(colon + 1)..].Trim();
        if (CellAddress.TryParse(first, out var a) && TryParseCorner(second, out var row, out var column))
        {
            range = new CellRange(Math.Min(a.Row, row), Math.Min(a.Column, column), Math.Max(a.Row, row), Math.Max(a.Column, column));
            return true;
        }

        if (CellAddress.TryParseColumn(first, out var left))
        {
            if (second is "*")
            {
                range = Columns(left, End);
                return true;
            }

            if (CellAddress.TryParseColumn(second, out var right))
            {
                range = Columns(Math.Min(left, right), Math.Max(left, right));
                return true;
            }
        }

        if (CellAddress.TryParseRow(first, out var top))
        {
            if (second is "*")
            {
                range = Rows(top, End);
                return true;
            }

            if (CellAddress.TryParseRow(second, out var bottom))
            {
                range = Rows(Math.Min(top, bottom), Math.Max(top, bottom));
                return true;
            }
        }

        return false;
    }

    public bool Contains(int row, int column) => (row >= Top) && (row <= Bottom) && (column >= Left) && (column <= Right);

    public bool Contains(CellAddress address) => Contains(address.Row, address.Column);

    public bool Intersects(CellRange other) => (other.Top <= Bottom) && (other.Bottom >= Top) && (other.Left <= Right) && (other.Right >= Left);

    public bool TryClip(int rowCount, int columnCount, out CellRange clipped)
    {
        clipped = default;
        if ((Top >= rowCount) || (Left >= columnCount))
        {
            return false;
        }

        clipped = new CellRange(Top, Left, Math.Min(Bottom, rowCount - 1), Math.Min(Right, columnCount - 1));
        return true;
    }

    public override string ToString()
    {
        if (this == All)
        {
            return "*";
        }

        if (IsEntireColumns)
        {
            return $"{CellAddress.ColumnName(Left)}:{FormatColumn(Right)}";
        }

        if (IsEntireRows)
        {
            return $"{FormatRow(Top)}:{FormatRow(Bottom)}";
        }

        var topLeft = new CellAddress(Top, Left).ToString();
        if ((Top == Bottom) && (Left == Right))
        {
            return topLeft;
        }

        return $"{topLeft}:{FormatColumn(Right)}{FormatRow(Bottom)}";
    }

    private static bool TryParseCorner(ReadOnlySpan<char> text, out int row, out int column)
    {
        if (CellAddress.TryParse(text, out var address))
        {
            (row, column) = (address.Row, address.Column);
            return true;
        }

        row = End;
        column = End;
        if (text.Length < 2)
        {
            return false;
        }

        if (text[^1] == '*')
        {
            return (text[..^1] is "*") || CellAddress.TryParseColumn(text[..^1], out column);
        }

        return (text[0] == '*') && CellAddress.TryParseRow(text[1..], out row);
    }

    private static string FormatColumn(int column) => column == End ? "*" : CellAddress.ColumnName(column);

    private static string FormatRow(int row) => row == End ? "*" : (row + 1).ToString(CultureInfo.InvariantCulture);
}
