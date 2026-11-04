namespace MusselSheet;

using System.Globalization;

public readonly record struct CellAddress(int Row, int Column)
{
    public const int MaxColumns = 16384;

    public static CellAddress Parse(string text)
    {
        if (!TryParse(text, out var address))
        {
            throw new FormatException($"Invalid cell address: {text}");
        }

        return address;
    }

    public static bool TryParse(string? text, out CellAddress address) => TryParse(text.AsSpan(), out address);

    public static bool TryParse(ReadOnlySpan<char> text, out CellAddress address)
    {
        address = default;
        var span = text.Trim();
        var letters = CountLetters(span);
        if ((letters == 0) || (letters == span.Length) || !TryParseColumn(span[..letters], out var column))
        {
            return false;
        }

        if (!TryParseRow(span[letters..], out var row))
        {
            return false;
        }

        address = new CellAddress(row, column);
        return true;
    }

    public static string ColumnName(int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        var buffer = new char[8];
        var position = buffer.Length;
        for (var value = column + 1; value > 0; value = (value - 1) / 26)
        {
            buffer[--position] = (char)('A' + ((value - 1) % 26));
        }

        return new string(buffer, position, buffer.Length - position);
    }

    public static bool TryParseColumn(ReadOnlySpan<char> name, out int column)
    {
        column = -1;
        if (name.IsEmpty)
        {
            return false;
        }

        var value = 0;
        foreach (var ch in name)
        {
            if (!char.IsAsciiLetter(ch))
            {
                return false;
            }

            value = (value * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
            if (value > MaxColumns)
            {
                return false;
            }
        }

        column = value - 1;
        return true;
    }

    public static bool TryParseRow(ReadOnlySpan<char> text, out int row)
    {
        row = -1;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || (number < 1))
        {
            return false;
        }

        row = number - 1;
        return true;
    }

    public override string ToString() => ColumnName(Column) + (Row + 1).ToString(CultureInfo.InvariantCulture);

    private static int CountLetters(ReadOnlySpan<char> span)
    {
        var count = 0;
        while ((count < span.Length) && char.IsAsciiLetter(span[count]))
        {
            count++;
        }

        return count;
    }
}
