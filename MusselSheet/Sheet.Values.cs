namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class Sheet
{
    public object? GetValue(int row, int column) => rowList[row].Cells[column]?.Value;

    public T? GetValue<T>(int row, int column)
    {
        if (!TryGetValue<T>(row, column, out var value))
        {
            throw new InvalidCastException($"The value of cell {new CellAddress(row, column)} cannot be converted to {typeof(T).Name}.");
        }

        return value;
    }

    public bool TryGetValue<T>(int row, int column, out T? value) => CellText.TryConvert(GetValue(row, column), out value);

    public void SetValue(int row, int column, object? value)
    {
        Store(row, column, value, null, null);
        Raise(new SheetChangedEventArgs(SheetChangeKind.Values, CellRange.Cell(row, column)));
    }

    public string? GetError(int row, int column) => rowList[row].Cells[column]?.Error;

    public void SetError(int row, int column, string? message)
    {
        var sheetRow = rowList[row];
        var data = sheetRow.Cells[column];
        if (string.Equals(data?.Error, message, StringComparison.Ordinal))
        {
            return;
        }

        data ??= sheetRow.Cells[column] = new CellData();
        data.Error = message;
        if (data.IsEmpty)
        {
            sheetRow.Cells[column] = null;
        }

        sheetRow.ContentVersion++;
        Raise(new SheetChangedEventArgs(SheetChangeKind.Errors, CellRange.Cell(row, column)));
    }

    public string? GetInputText(int row, int column) => rowList[row].Cells[column]?.Input;

    public string GetText(int row, int column)
    {
        var resolved = GetSettings(row, column);
        var value = GetValue(row, column);
        var text = CellText.Display(value, resolved, Culture, Options.DateFormatFor(Culture), Options.DatesFor(Culture));
        return resolved.Formatter?.Invoke(new CellFormatContext(this, new CellAddress(row, column), value, text)) ?? text;
    }

    public string GetEditText(int row, int column) =>
        GetInputText(row, column) ?? CellText.Edit(GetValue(row, column), GetSettings(row, column), Culture, Options.DatesFor(Culture));

    public object?[][] GetValues(CellRange range)
    {
        if (!range.TryClip(RowCount, ColumnCount, out var clipped))
        {
            return [];
        }

        var result = new object?[clipped.Bottom - clipped.Top + 1][];
        for (var r = 0; r < result.Length; r++)
        {
            var cells = rowList[clipped.Top + r].Cells;
            var values = new object?[clipped.Right - clipped.Left + 1];
            for (var c = 0; c < values.Length; c++)
            {
                values[c] = cells[clipped.Left + c]?.Value;
            }

            result[r] = values;
        }

        return result;
    }

    public void SetValues(int top, int left, IReadOnlyList<IReadOnlyList<object?>> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        var width = values.Max(x => x.Count);
        if ((top + values.Count > RowCount) || (left + width > ColumnCount))
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The values extend beyond the sheet.");
        }

        for (var r = 0; r < values.Count; r++)
        {
            for (var c = 0; c < values[r].Count; c++)
            {
                Store(top + r, left + c, values[r][c], null, null);
            }
        }

        Raise(new SheetChangedEventArgs(SheetChangeKind.Values, new CellRange(top, left, top + values.Count - 1, left + Math.Max(width, 1) - 1)));
    }

    public void ClearContents(CellRange range)
    {
        if (!range.TryClip(RowCount, ColumnCount, out var clipped))
        {
            return;
        }

        for (var r = clipped.Top; r <= clipped.Bottom; r++)
        {
            for (var c = clipped.Left; c <= clipped.Right; c++)
            {
                if (rowList[r].Cells[c] is not null)
                {
                    Store(r, c, null, null, null);
                }
            }

            rowList[r].ContentVersion++;
        }

        Raise(new SheetChangedEventArgs(SheetChangeKind.Values, clipped));
    }

    public bool IsChanged(int row, int column) => rowList[row].Cells[column] is { HasOriginal: true };

    public object? GetOriginalValue(int row, int column)
    {
        var data = rowList[row].Cells[column];
        return data is { HasOriginal: true } ? data.Original : data?.Value;
    }

    public IReadOnlyList<CellAddress> GetChangedCells()
    {
        var result = new List<CellAddress>();
        for (var r = 0; r < rowList.Count; r++)
        {
            var cells = rowList[r].Cells;
            for (var c = 0; c < cells.Length; c++)
            {
                if (cells[c] is { HasOriginal: true })
                {
                    result.Add(new CellAddress(r, c));
                }
            }
        }

        return result;
    }

    public void AcceptChanges()
    {
        var accepted = false;
        foreach (var row in rowList)
        {
            var cells = row.Cells;
            for (var c = 0; c < cells.Length; c++)
            {
                if (cells[c] is not { HasOriginal: true } data)
                {
                    continue;
                }

                data.HasOriginal = false;
                data.Original = null;
                if (data.IsEmpty)
                {
                    cells[c] = null;
                }

                row.ContentVersion++;
                accepted = true;
            }
        }

        if (accepted)
        {
            Raise(new SheetChangedEventArgs(SheetChangeKind.Changes));
        }
    }

    internal CellSnapshot GetSnapshot(int row, int column)
    {
        var data = rowList[row].Cells[column];
        return data is null ? default : new CellSnapshot(data.Value, data.Input, data.Error);
    }

    // A user action: the first time a cell changes, its previous value is remembered as the original (change mark)
    internal void SetInput(int row, int column, object? value, string? input, string? error = null)
    {
        var cells = rowList[row].Cells;
        if (cells[column] is not { HasOriginal: true })
        {
            var data = cells[column] ??= new CellData();
            data.Original = data.Value;
            data.HasOriginal = true;
        }

        Store(row, column, value, input, error);
        Raise(new SheetChangedEventArgs(SheetChangeKind.Values, CellRange.Cell(row, column)));
    }

    // The change mark is cleared as soon as the value equals the original, including when the app set it
    private void Store(int row, int column, object? value, string? input, string? error)
    {
        var sheetRow = rowList[row];
        sheetRow.ContentVersion++;
        var data = sheetRow.Cells[column];
        if ((data is null) && (value is null) && (input is null) && (error is null))
        {
            return;
        }

        data ??= sheetRow.Cells[column] = new CellData();
        data.Value = value;
        data.Input = input;
        data.Error = error;
        if (data.HasOriginal && Equals(data.Original, value))
        {
            data.HasOriginal = false;
            data.Original = null;
        }

        if (data.IsEmpty)
        {
            sheetRow.Cells[column] = null;
        }
    }
}
