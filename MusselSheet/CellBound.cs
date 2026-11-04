namespace MusselSheet;

using System.Globalization;

public readonly record struct CellBound
{
    private CellBound(decimal? number, DateOnly? date, TimeOnly? time)
    {
        Number = number;
        Date = date;
        Time = time;
    }

    public decimal? Number { get; }

    public DateOnly? Date { get; }

    public TimeOnly? Time { get; }

    public static implicit operator CellBound(decimal value) => FromDecimal(value);

    public static implicit operator CellBound(int value) => FromInt32(value);

    public static implicit operator CellBound(DateOnly value) => FromDateOnly(value);

    public static implicit operator CellBound(TimeOnly value) => FromTimeOnly(value);

    public static CellBound FromDecimal(decimal value) => new(value, null, null);

    public static CellBound FromInt32(int value) => new(value, null, null);

    public static CellBound FromDateOnly(DateOnly value) => new(null, value, null);

    public static CellBound FromTimeOnly(TimeOnly value) => new(null, null, value);

    public override string ToString()
    {
        if (Number is { } number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        if (Date is { } date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return Time?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
