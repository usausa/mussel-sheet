namespace MusselSheet;

using System.Globalization;

using Microsoft.AspNetCore.Components;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    internal void OnViewportChanged(int top, int height, int left, int width)
    {
        scrollTop = Math.Max(0, top);
        viewportHeight = Math.Max(0, height);
        scrollLeft = Math.Max(0, left);
        viewportWidth = Math.Max(0, width);
        if (UpdateRange())
        {
            StateHasChanged();
        }
    }

    internal static string RowStyle(int height) =>
        string.Create(CultureInfo.InvariantCulture, $"height:{height}px;line-height:{height - 1}px;--mussel-sheet-row-h:{height}px");

    internal CellView GetCellView(int row, int column)
    {
        // A merged range is drawn by its top-left cell at the size of the range; the other cells draw nothing.
        // The right and bottom lines come from the range's top-right and bottom-left cells (its outer edges).
        var merged = Sheet.GetMergeArea(row, column);
        if ((merged is { } covered) && ((covered.Top != row) || (covered.Left != column)))
        {
            return CellView.CoveredCell;
        }

        var settings = Sheet.GetSettings(row, column);
        var value = Sheet.GetValue(row, column);
        var text = CellText.Display(value, settings, Sheet.Culture, DateFormat, Dates);

        var format = (settings.Formatter is not null) || (settings.StyleProvider is not null) ? new CellFormatContext(Sheet, new CellAddress(row, column), value, text) : null;
        var patch = format is null ? null : settings.StyleProvider?.Invoke(format);
        if ((format is not null) && (settings.Formatter?.Invoke(format) is { } formatted))
        {
            text = formatted;
        }

        var borders = settings.Style.Borders;
        var edges = merged is { } m ? new CellEdges(Sheet.GetEdges(m.Top, m.Right).Right, Sheet.GetEdges(m.Bottom, m.Left).Bottom) : Sheet.GetEdges(row, column);
        var css = styles.GetCellClass(settings, edges, column == 0 ? borders.Left : null, row == 0 ? borders.Top : null);
        if (merged is { } range)
        {
            css += " mussel-sheet-merge " + styles.GetMergeClass(range);
        }

        if (!string.IsNullOrWhiteSpace(patch?.CssClass))
        {
            css += " " + patch.CssClass.Trim();
        }

        var error = Sheet.GetError(row, column);
        if (error is not null)
        {
            css += " mussel-sheet-error";
        }

        if (ShowChanges && Sheet.IsChanged(row, column))
        {
            css += " mussel-sheet-changed";
        }

        // The box is drawn with CSS and also holds a visually hidden ☑ / ☐ so screen readers announce the state.
        // Only a click on this element toggles.
        string? check = null;
        if (settings.Kind == CellKind.Checkbox)
        {
            var on = CellText.IsChecked(value);
            if (CellText.HasCheckText(settings))
            {
                check = on ? "mussel-sheet-ct mussel-sheet-on" : "mussel-sheet-ct";
            }
            else
            {
                check = on ? "mussel-sheet-cb mussel-sheet-on" : "mussel-sheet-cb";
            }
        }

        // Edit text is emitted only when it differs from the display text. A custom display is drawn by the app's
        // template, so its edit text is always emitted.
        var custom = (settings.Kind == CellKind.Custom) && (CustomDisplay is not null);
        var editText = Sheet.GetEditText(row, column);
        var edit = custom || !string.Equals(editText, text, StringComparison.Ordinal) ? editText : null;

        return new CellView(
            css,
            text,
            settings.Kind == CellKind.Text ? null : SheetMapBuilder.KindName(settings.Kind),
            settings.IsInputDisabled ? "1" : null,
            settings.ImeMode == ImeMode.Off ? "half" : null,
            edit,
            error,
            check,
            custom,
            merged is { } span && (span.Bottom > span.Top) ? span.Bottom - span.Top + 1 : null,
            merged is { } wide && (wide.Right > wide.Left) ? wide.Right - wide.Left + 1 : null,
            Style: patch is null ? null : SheetStyles.GetValueStyle(settings, patch));
    }

    // A row holding the top-left cell of a merge that spans rows must not skip painting (content-visibility)
    internal bool HasTallMerge(int row) =>
        Sheet.MergeAreas.Any(x => (x.Top == row) && (x.Bottom > x.Top) && (x.Left >= FrozenColumnCount) && (x.Left <= RestLast));

    // aria-rowindex is 1-based and the column header row counts as a row
    internal int AriaRowIndex(int row) => row + (ShowColumnHeaders ? 2 : 1);

    internal int AriaColumnIndex(int column) => column + (ShowRowHeaders ? 2 : 1);

    internal RenderFragment RenderCustomDisplay(int row, int column)
    {
        var settings = Sheet.GetSettings(row, column);
        var value = Sheet.GetValue(row, column);
        var text = CellText.Display(value, settings, Sheet.Culture, DateFormat, Dates);
        text = settings.Formatter?.Invoke(new CellFormatContext(Sheet, new CellAddress(row, column), value, text)) ?? text;
        var context = new CellDisplayContext(Sheet, new CellAddress(row, column), settings.Editor, value, text);
        return CustomDisplay is { } display ? display(context) : builder => builder.AddContent(0, context.Text);
    }

    private static bool IsAuto(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);

    private static string SpacerStyle(int height) => string.Create(CultureInfo.InvariantCulture, $"height:{height}px");

    private void Refresh()
    {
        if (layoutVersion != Sheet.StructureVersion)
        {
            layoutVersion = Sheet.StructureVersion;
            rowLayout.Reset(Sheet.RowCount, i => Sheet.Rows[i].ActualHeight);
            columnLayout.Reset(Sheet.ColumnCount, i => Sheet.Columns[i].ActualWidth);
            contentWidth = HeaderWidth + columnLayout.Total;
            styles.Update(Sheet, HeaderWidth, FrozenColumnCount);
            UpdateColumns(true);
        }

        UpdateRange();
    }

    private bool UpdateRange()
    {
        var rows = UpdateRows();
        var columns = UpdateColumns(false);
        return rows || columns;
    }

    // A changed range bumps ColumnVersion so rows re-render, and rebuilds the CSS of the rest container
    private bool UpdateColumns(bool force)
    {
        var frozenWidth = columnLayout.Start(Math.Min(FrozenColumnCount, columnLayout.Count));
        var (first, last) = IsColumnVirtualized
            ? columnLayout.GetRange(scrollLeft + frozenWidth, Math.Max(1, ViewportWidth() - HeaderWidth - frozenWidth), Math.Max(0, OverscanColumnCount))
            : (0, Sheet.ColumnCount - 1);
        first = IncludeMergeStarts(first, last, FrozenColumnCount, x => (x.Left, x.Right));
        if (!force && (first == firstColumn) && (last == lastColumn))
        {
            return false;
        }

        firstColumn = first;
        lastColumn = last;
        ColumnVersion++;
        styles.UpdateColumns(Sheet, FrozenColumnCount, RestFirst, RestLast);
        return true;
    }

    private bool UpdateRows()
    {
        var frozenHeight = rowLayout.Start(Math.Min(FrozenRowCount, rowLayout.Count));
        var (first, last) = IsVirtualized
            ? rowLayout.GetRange(scrollTop + frozenHeight, Math.Max(1, ViewportHeight() - HeaderHeight - frozenHeight), Math.Max(0, OverscanCount))
            : (0, Sheet.RowCount - 1);
        first = IncludeMergeStarts(first, last, FrozenRowCount, x => (x.Top, x.Bottom));
        if ((first == firstRow) && (last == lastRow))
        {
            return false;
        }

        firstRow = first;
        lastRow = last;
        return true;
    }

    // Extends the start of the drawn range back to the top-left cell of any overlapping merge, because a merge
    // is drawn by its top-left cell. A top-left cell in the frozen part is always drawn, so it needs no extension.
    private int IncludeMergeStarts(int first, int last, int frozen, Func<CellRange, (int Start, int End)> span)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            var start = Math.Max(first, frozen);
            foreach (var range in Sheet.MergeAreas)
            {
                var (from, to) = span(range);
                if ((from < start) && (from >= frozen) && (to >= start) && (from <= last))
                {
                    first = from;
                    changed = true;
                    break;
                }
            }
        }

        return first;
    }

    private int ViewportWidth()
    {
        if (viewportWidth > 0)
        {
            return viewportWidth;
        }

        var text = Width.Trim();
        return text.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text.AsSpan(0, text.Length - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var px)
            ? px
            : DefaultViewportWidth;
    }

    private int ViewportHeight()
    {
        if (viewportHeight > 0)
        {
            return viewportHeight;
        }

        var text = Height.Trim();
        return text.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text.AsSpan(0, text.Length - 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var px)
            ? px
            : DefaultViewportHeight;
    }

    // Scroll positions at which JS must report back so the drawn rows or columns can change. They sit half the
    // overscan inside the drawn range, so the report comes before its edge is reached. The upper/left bound is
    // measured past the frozen part, and a direction that is not virtualized never triggers.
    private (int Top, int Bottom, int Left, int Right) Thresholds()
    {
        var (top, bottom) = (int.MinValue, int.MaxValue);
        if (IsVirtualized && (lastRow >= firstRow))
        {
            var margin = Math.Max(1, OverscanCount) * Sheet.DefaultRowHeight / 2;
            var frozen = FrozenRowCount;
            top = BodyFirst > frozen ? rowLayout.Start(BodyFirst) - rowLayout.Start(frozen) + margin : int.MinValue;
            bottom = lastRow < Sheet.RowCount - 1 ? HeaderHeight + rowLayout.End(lastRow) - margin : int.MaxValue;
        }

        var (left, right) = (int.MinValue, int.MaxValue);
        if (IsColumnVirtualized && (lastColumn >= firstColumn))
        {
            var margin = Math.Max(1, OverscanColumnCount) * Sheet.DefaultColumnWidth / 2;
            var frozen = FrozenColumnCount;
            left = RestFirst > frozen ? columnLayout.Start(RestFirst) - columnLayout.Start(frozen) + margin : int.MinValue;
            right = lastColumn < Sheet.ColumnCount - 1 ? HeaderWidth + columnLayout.End(lastColumn) - margin : int.MaxValue;
        }

        return (top, bottom, left, right);
    }
}
