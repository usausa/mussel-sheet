namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    internal bool OpenCustomEditor(int row, int column)
    {
        if ((CustomEditor is null) || !IsEditable(row, column) || (Sheet.GetSettings(row, column).Kind != CellKind.Custom))
        {
            return false;
        }

        customEdit = new CellEditContext(
            Sheet,
            new CellAddress(row, column),
            Sheet.GetSettings(row, column).Editor,
            Sheet.GetValue(row, column),
            CommitCustomFromTemplateAsync,
            CancelCustomFromTemplateAsync);
        customPending = true;
        StateHasChanged();
        return true;
    }

    internal Task<CustomEditorResult> CommitCustomEditorAsync() =>
        customEdit is { } edit ? CommitCustomCoreAsync(edit) : Task.FromResult(new CustomEditorResult(true, null));

    internal void CancelCustomEditor() => CloseCustom();

    private async Task<CustomEditorResult> CommitCustomCoreAsync(CellEditContext edit)
    {
        var (row, column) = (edit.Address.Row, edit.Address.Column);
        var result = await CommitValueAsync(row, column, edit.Value).ConfigureAwait(true);
        var keepOpen = !result.Stored
            && (result.Error is not null)
            && IsEditable(row, column)
            && ((Sheet.GetSettings(row, column).ValidationMode ?? ValidationMode) != SheetValidationMode.Revert);
        if (keepOpen && ReferenceEquals(edit, customEdit))
        {
            edit.Error = result.Error;
            StateHasChanged();
            return new CustomEditorResult(false, result.Error);
        }

        if (ReferenceEquals(edit, customEdit))
        {
            CloseCustom();
        }

        return new CustomEditorResult(true, result.Error);
    }

    // Once closed, JS returns focus to the input element; the active cell does not move
    private async Task<bool> CommitCustomFromTemplateAsync(CellEditContext edit)
    {
        if (!ReferenceEquals(edit, customEdit))
        {
            return false;
        }

        var result = await CommitCustomCoreAsync(edit).ConfigureAwait(true);
        if (result.Closed)
        {
            await InvokeScriptAsync("finishCustom").ConfigureAwait(true);
        }

        return result.Closed && (result.Error is null);
    }

    private async Task CancelCustomFromTemplateAsync()
    {
        if (customEdit is null)
        {
            return;
        }

        CloseCustom();
        await InvokeScriptAsync("finishCustom").ConfigureAwait(true);
    }

    private void CloseCustom()
    {
        if (customEdit is null)
        {
            return;
        }

        customEdit = null;
        customPending = false;
        StateHasChanged();
    }
}
