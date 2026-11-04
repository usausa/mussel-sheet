namespace MusselSheet;

public sealed class SheetRange
{
    internal SheetRange(Sheet sheet, CellRange range)
    {
        Sheet = sheet;
        Range = range;
    }

    public Sheet Sheet { get; }

    public CellRange Range { get; }

    public void Configure(Action<CellSettings> configure) => Sheet.Configure(Range, configure);

    public object?[][] GetValues() => Sheet.GetValues(Range);

    public void SetValues(IReadOnlyList<IReadOnlyList<object?>> values) => Sheet.SetValues(Range.Top, Range.Left, values);

    public void ClearContents() => Sheet.ClearContents(Range);

    public void Merge() => Sheet.Merge(Range);

    public void Unmerge() => Sheet.Unmerge(Range);

    public void BorderAround(CellBorder line)
    {
        using var scope = Sheet.BeginUpdate();
        var r = Range;
        Sheet.Configure(new CellRange(r.Top, r.Left, r.Top, r.Right), c => c.Style.Borders.Top = line);
        Sheet.Configure(new CellRange(r.Top, r.Left, r.Bottom, r.Left), c => c.Style.Borders.Left = line);
        if (r.Bottom != CellRange.End)
        {
            Sheet.Configure(new CellRange(r.Bottom, r.Left, r.Bottom, r.Right), c => c.Style.Borders.Bottom = line);
        }

        if (r.Right != CellRange.End)
        {
            Sheet.Configure(new CellRange(r.Top, r.Right, r.Bottom, r.Right), c => c.Style.Borders.Right = line);
        }
    }

    public override string ToString() => Range.ToString();
}
