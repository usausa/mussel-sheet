namespace MusselSheet;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    internal Task<CommitResult> CommitAsync(int row, int column, string text, bool optimistic) =>
        CommitCoreAsync(row, column, text, optimistic, CellChangeReason.Input);

    // Delete clears without validation, same as Excel
    internal Task<CommitResult> ClearAsync(int row, int column) =>
        CommitCoreAsync(row, column, string.Empty, true, CellChangeReason.ClearContents);

    internal Task<CommitResult> ToggleAsync(int row, int column)
    {
        if (!IsEditable(row, column) || (Sheet.GetSettings(row, column).Kind != CellKind.Checkbox))
        {
            return Task.FromResult(Rejected);
        }

        var current = CellText.IsChecked(Sheet.GetValue(row, column));
        return CommitCoreAsync(row, column, current ? "false" : "true", true, CellChangeReason.Toggle);
    }

    internal string GetEditText(int row, int column) => IsInSheet(row, column) ? Sheet.GetEditText(row, column) : string.Empty;

    // A newer request cancels the previous one, which then returns null
    internal async Task<IReadOnlyList<string>?> GetSuggestionsAsync(int row, int column, string text)
    {
        if (suggestionCancel is { } previous)
        {
            await previous.CancelAsync().ConfigureAwait(true);
        }

        using var cancel = new CancellationTokenSource();
        suggestionCancel = cancel;
        try
        {
            if (!IsEditable(row, column) || (Sheet.GetSettings(row, column).SuggestionProvider is not { } provider))
            {
                return null;
            }

            var items = await provider(new CellSuggestionContext(Sheet, new CellAddress(row, column), text, cancel.Token)).ConfigureAwait(true);
            return cancel.IsCancellationRequested ? null : [.. items.Take(MaxSuggestions)];
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            if (ReferenceEquals(suggestionCancel, cancel))
            {
                suggestionCancel = null;
            }
        }
    }

    private Task<CommitResult> CommitCoreAsync(int row, int column, string text, bool optimistic, CellChangeReason reason)
    {
        if (!IsEditable(row, column))
        {
            return Task.FromResult(Rejected);
        }

        var draft = reason is CellChangeReason.ClearContents or CellChangeReason.Cut ? CommitDraft.Clear : Draft(row, column, text);
        return ApplyAsync(row, column, draft, optimistic, reason);
    }

    private CommitDraft Draft(int row, int column, string text)
    {
        var settings = Sheet.GetSettings(row, column);
        var context = InputContext;
        var normalized = CellInput.Normalize(text, settings, context.Reading);
        var corrected = false;
        var error = CellInput.Check(normalized, settings, context, out var value, out var converted, out var omitsYear, settings.Corrector is null ? null : Correct);
        var checkedText = corrected ? CellText.Edit(value, settings, Sheet.Culture, context.Reading) : normalized;
        error ??= RunValidator(settings, row, column, value, checkedText);
        error ??= BindingError(row, column, value);

        // A year-less date or a corrected value drops the typed text, so re-editing shows the full value
        var yearless = (settings.Kind == CellKind.Date) && converted && omitsYear;
        return new CommitDraft(checkedText, value, converted, error, (normalized.Length == 0) || yearless || corrected ? null : normalized);

        object? Correct(object? input)
        {
            var output = settings.Corrector(new CellCorrectionContext(Sheet, new CellAddress(row, column), input, normalized));
            corrected = !Equals(output, input);
            return output;
        }
    }

    private static bool IsTextKind(CellKind kind) => kind is CellKind.Text or CellKind.Mask or CellKind.AutoComplete or CellKind.List;

    private Task<CommitResult> CommitValueAsync(int row, int column, object? value, bool optimistic)
    {
        if (!IsEditable(row, column))
        {
            return Task.FromResult(Rejected);
        }

        var settings = Sheet.GetSettings(row, column);
        var text = CellText.Edit(value, settings, Sheet.Culture, Reading);
        if (settings.Corrector is { } corrector)
        {
            value = corrector(new CellCorrectionContext(Sheet, new CellAddress(row, column), value, text));
            text = CellText.Edit(value, settings, Sheet.Culture, Reading);
        }

        var error = CellInput.Validate(value, text, settings, InputContext);
        error = error is null ? RunValidator(settings, row, column, value, text) : settings.ErrorMessage ?? error;
        error ??= BindingError(row, column, value);
        return ApplyAsync(row, column, new CommitDraft(text, value, true, error, null), optimistic, CellChangeReason.Input);
    }

    private string? RunValidator(ResolvedCellSettings settings, int row, int column, object? value, string text) =>
        settings.Validator?.Invoke(new CellValidationContext(Sheet, new CellAddress(row, column), value, text));

    private string? BindingError(int row, int column, object? value)
    {
        if (Sheet.FindBinding(row, column) is not { } bound)
        {
            return null;
        }

        var property = bound.Property;
        if (!property.CanWrite)
        {
            return Texts.NotEditable;
        }

        if (property.TryConvert(value, bound.Target, out _))
        {
            return null;
        }

        var type = property.ValueType;
        return type switch
        {
            _ when BoundProperty.IsWholeNumber(type) => Texts.WholeNumber,
            _ when BoundProperty.IsNumber(type) => Texts.Number,
            _ when type == typeof(DateTimeOffset) => Texts.NotEditable,
            _ when (type == typeof(DateOnly)) || (type == typeof(DateTime)) => InputContext.DateMessage,
            _ when (type == typeof(TimeOnly)) || (type == typeof(TimeSpan)) => Texts.Time,
            _ when type == typeof(bool) => Texts.Check,
            _ => Texts.Pattern
        };
    }

    // optimistic: the user has left the cell, so a Stay error is stored with a mark; batch makes one undo step
    private async Task<CommitResult> ApplyAsync(int row, int column, CommitDraft draft, bool optimistic, CellChangeReason reason, List<CellChange>? batch = null)
    {
        var (sheetRow, sheetColumn) = (Sheet.Rows[row], Sheet.Columns[column]);
        var (value, error) = (draft.Value, draft.Error);
        var committing = new CellCommittingEventArgs(Sheet, new CellAddress(row, column), draft.Text, value, error, reason);
        await HandleAsync(CellCommitting, committing).ConfigureAwait(true);

        // The handler can insert or delete rows and columns, so the cell is found again
        if (!Sheet.Contains(sheetRow) || !Sheet.Contains(sheetColumn) || !IsEditable(sheetRow.Index, sheetColumn.Index))
        {
            return Rejected;
        }

        (row, column) = (sheetRow.Index, sheetColumn.Index);
        var settings = Sheet.GetSettings(row, column);

        if (committing.Cancel)
        {
            // On cancel nothing is applied; if optimistic, the cell is marked so the failure stays visible
            var message = committing.Error ?? Texts.Canceled;
            if (optimistic)
            {
                Sheet.SetError(row, column, message);
            }

            return new CommitResult(false, message, false);
        }

        value = committing.Value;
        error = committing.Error;
        var policy = settings.ValidationMode ?? ValidationMode;
        if ((error is not null) && ((policy == SheetValidationMode.Revert) || ((policy == SheetValidationMode.Stay) && !optimistic)))
        {
            return new CommitResult(false, error, false);
        }

        // Unconvertible text is kept as a string so it can still be corrected later
        if ((error is not null) && !draft.Converted && (value is null))
        {
            value = draft.Text;
        }

        // Nothing to do when value and error are unchanged; a different typed text alone is not a change
        var before = Sheet.GetSnapshot(row, column);
        var after = new CellSnapshot(value, draft.Input, error);
        if (Equals(before.Value, after.Value) && String.Equals(before.Error, after.Error, StringComparison.Ordinal))
        {
            return new CommitResult(error is null, error, true);
        }

        var generation = history.Generation;
        Sheet.SetInput(row, column, after.Value, after.Input, after.Error);
        var change = new CellChange(sheetRow, sheetColumn, before, after);
        if (batch is null)
        {
            if (generation == history.Generation)
            {
                history.Record(new CellsEdited([change]));
            }
        }
        else
        {
            batch.Add(change);
        }

        await HandleAsync(CellCommitted, new CellCommittedEventArgs(Sheet, new CellAddress(sheetRow.Index, sheetColumn.Index), before.Value, value, error, reason)).ConfigureAwait(true);
        return new CommitResult(error is null, error, true);
    }

    private readonly record struct CommitDraft(string Text, object? Value, bool Converted, string? Error, string? Input)
    {
        public static CommitDraft Clear { get; } = new(string.Empty, null, true, null, null);
    }
}
