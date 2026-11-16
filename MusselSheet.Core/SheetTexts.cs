namespace MusselSheet;

public sealed record SheetTexts
{
    public static SheetTexts English { get; } = new();

    public string Required { get; init; } = "Enter a value.";

    public string Number { get; init; } = "Enter a number.";

    public string WholeNumber { get; init; } = "Enter a whole number.";

    public string Date { get; init; } = "Enter a date in the form {0}.";

    public string Time { get; init; } = "Enter a time in the form HH:mm.";

    public string Check { get; init; } = "Enter true or false.";

    public string List { get; init; } = "Choose a value from the list.";

    public string Between { get; init; } = "Enter a value from {0} to {1}.";

    public string AtLeast { get; init; } = "Enter a value of {0} or more.";

    public string AtMost { get; init; } = "Enter a value of {0} or less.";

    public string MaxLength { get; init; } = "Enter {0} characters or fewer.";

    public string MaxByteLength { get; init; } = "Enter {0} bytes or fewer (full-width characters count as 2).";

    public string Pattern { get; init; } = "The value is not in the expected format.";

    public string Mask { get; init; } = "Enter a value in the form {0}.";

    public string Characters { get; init; } = "Enter only the allowed characters.";

    public IReadOnlyDictionary<string, string> CharacterSets { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string NotEditable { get; init; } = "This cell cannot be edited.";

    public string Canceled { get; init; } = "The value could not be entered.";

    public string PasteMarked { get; init; } = "{0} cell(s) did not match their rules and were marked as errors.";

    public string PasteRejected { get; init; } = "{0} cell(s) did not match their rules and were not pasted.";

    public string PasteTruncatedRows { get; init; } = "{0} row(s) outside the sheet were not pasted.";

    public string PasteTruncatedColumns { get; init; } = "{0} column(s) outside the sheet were not pasted.";

    public string PasteTruncatedBoth { get; init; } = "{0} row(s) and {1} column(s) outside the sheet were not pasted.";

    public string MessageSeparator { get; init; } = " ";

    public string MenuCut { get; init; } = "Cut";

    public string MenuCopy { get; init; } = "Copy";

    public string MenuPaste { get; init; } = "Paste";

    public string MenuInsertRowsAbove { get; init; } = "Insert {0} row(s) above";

    public string MenuInsertRowsBelow { get; init; } = "Insert {0} row(s) below";

    public string MenuDeleteRows { get; init; } = "Delete {0} row(s)";

    public string MenuInsertColumnsLeft { get; init; } = "Insert {0} column(s) left";

    public string MenuInsertColumnsRight { get; init; } = "Insert {0} column(s) right";

    public string MenuDeleteColumns { get; init; } = "Delete {0} column(s)";

    public string MenuPasteFailed { get; init; } = "Could not paste from the menu. Press Ctrl+V to paste.";

    public string CalendarToday { get; init; } = "Today";

    public string CalendarPrevious { get; init; } = "Previous month";

    public string CalendarNext { get; init; } = "Next month";

    public string CellInput { get; init; } = "Cell input";
}
