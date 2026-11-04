namespace MusselSheet;

using System.Globalization;

public readonly record struct CellRange
{
    public const int End = int.MaxValue;

    public CellRange(int top, int left, int bottom, int right)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        ArgumentOutOfRangeException.ThrowIfLessThan(bottom, top);
        ArgumentOutOfRangeException.ThrowIfLessThan(right, left);
        Top = top;
        Left = left;
        Bottom = bottom;
        Right = right;
    }

    public static CellRange All { get; } = new(0, 0, End, End);

    public int Top { get; }

    public int Left { get; }

    public int Bottom { get; }

    public int Right { get; }

    public bool IsEntireColumns => (Top == 0) && (Bottom == End);

    public bool IsEntireRows => (Left == 0) && (Right == End);

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
        if (CellAddress.TryParse(first, out var a) && CellAddress.TryParse(second, out var b))
        {
            range = new CellRange(Math.Min(a.Row, b.Row), Math.Min(a.Column, b.Column), Math.Max(a.Row, b.Row), Math.Max(a.Column, b.Column));
            return true;
        }

        if (CellAddress.TryParseColumn(first, out var left) && CellAddress.TryParseColumn(second, out var right))
        {
            range = Columns(Math.Min(left, right), Math.Max(left, right));
            return true;
        }

        if (CellAddress.TryParseRow(first, out var top) && CellAddress.TryParseRow(second, out var bottom))
        {
            range = Rows(Math.Min(top, bottom), Math.Max(top, bottom));
            return true;
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

    private static string FormatColumn(int column) => column == End ? "*" : CellAddress.ColumnName(column);

    private static string FormatRow(int row) => row == End ? "*" : (row + 1).ToString(CultureInfo.InvariantCulture);
}
