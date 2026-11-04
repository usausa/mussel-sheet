namespace Example.Client.Components.Pages;

public sealed partial class PlaygroundPage
{
    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    private MusselSheetView? view;

    private bool showRowHeaders = true;

    private bool showColumnHeaders = true;

    private bool showGridLines = true;

    private bool showChanges;

    private bool fillHandleEnabled = true;

    private bool tabReturnEnabled = true;

    private bool allowRows = true;

    private bool allowColumns;

    private string height = "auto";

    private SheetMoveDirection moveAfterReturnDirection = SheetMoveDirection.Down;

    private SheetReadyEnterMode readyEnterMode = SheetReadyEnterMode.Move;

    private SheetRowEndMode rowEndMode = SheetRowEndMode.NextRow;

    private SheetSkipReadOnlyMode skipReadOnlyMode = SheetSkipReadOnlyMode.EnterAndTab;

    private SheetLastRowEnterMode lastRowEnterMode = SheetLastRowEnterMode.Stop;

    private SheetCommitMode commitMode = SheetCommitMode.Optimistic;

    private SheetValidationMode validationMode = SheetValidationMode.Stay;

    private decimal changed;

    // These belong to the sheet, so they are also saved with its design
    private int FrozenRows
    {
        get => sheet.FrozenRows;
        set => sheet.FrozenRows = value;
    }

    private int FrozenColumns
    {
        get => sheet.FrozenColumns;
        set => sheet.FrozenColumns = value;
    }

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(12, 5);
        string[] headers = ["Item", "Quantity", "Unit price", "Amount", "Date"];
        int[] widths = [140, 100, 100, 100, 120];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Columns[c].Header = headers[c];
                sheet.Columns[c].Width = widths[c];
            }

            sheet.Range("B:C").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
            });
            sheet.Range("D:D").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.ReadOnly = true;
            });
            sheet.Range("E:E").Configure(static c => c.Kind = CellKind.Date);
            for (var r = 0; r < sheet.RowCount; r++)
            {
                var quantity = (decimal)(r + 1);
                var price = 100m * (r + 1);
                sheet.SetValues(r, 0, [[$"Item {r + 1}", quantity, price, quantity * price, new DateOnly(2026, 11, r + 1)]]);
            }
        }

        return sheet;
    }

    private async Task UndoAsync()
    {
        if (view is not null)
        {
            await view.UndoAsync();
        }
    }

    private async Task RedoAsync()
    {
        if (view is not null)
        {
            await view.RedoAsync();
        }
    }

    private Task FocusAsync() => view?.FocusAsync() ?? Task.CompletedTask;

    private Task MoveAsync() => view?.SetActiveCellAsync(1, 2) ?? Task.CompletedTask;

    private Task BeginEditAsync() => view?.BeginEditAsync() ?? Task.CompletedTask;

    private Task CommitEditAsync() => view?.CommitEditAsync() ?? Task.CompletedTask;

    private Task CancelEditAsync() => view?.CancelEditAsync() ?? Task.CompletedTask;

    private void AddRow() => sheet.AddRows(1);

    private void InsertRow() => sheet.InsertRows(Math.Min(1, sheet.RowCount), 1);

    private void DeleteFirstRow()
    {
        if (sheet.RowCount > 0)
        {
            sheet.DeleteRows(0, 1);
        }
    }

    private void ToggleError() => sheet.SetError(0, 1, sheet.GetError(0, 1) is null ? "An error mark set from C#" : null);

    private void ChangeValue() => sheet.SetValue(0, 1, changed += 1000m);

    private void AcceptChanges()
    {
        sheet.AcceptChanges();
        log.Add("AcceptChanges");
    }
}
