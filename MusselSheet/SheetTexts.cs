namespace MusselSheet;

public sealed record SheetTexts
{
    public static SheetTexts English { get; } = new();

    public static SheetTexts Japanese { get; } = new()
    {
        Required = "入力してください",
        Number = "数値を入力してください",
        WholeNumber = "整数を入力してください",
        Date = "日付を {0} の形で入力してください",
        Time = "時刻を HH:mm の形で入力してください",
        Check = "true か false を入力してください",
        List = "一覧の中から選んでください",
        Between = "{0} から {1} の範囲で入力してください",
        AtLeast = "{0} 以上で入力してください",
        AtMost = "{0} 以下で入力してください",
        MaxLength = "{0} 文字以内で入力してください",
        MaxByteLength = "{0} バイト以内で入力してください(全角は2バイト)",
        Pattern = "入力の形式が正しくありません",
        Mask = "{0} の形で入力してください",
        Fullwidth = "全角で入力してください",
        Halfwidth = "半角で入力してください",
        Hiragana = "ひらがなで入力してください",
        Katakana = "全角カタカナで入力してください",
        HalfwidthKatakana = "半角カタカナで入力してください",
        Alphanumeric = "半角英数字で入力してください",
        Digits = "半角数字で入力してください",
        NotEditable = "入力できないセルです",
        Canceled = "入力できませんでした",
        PasteMarked = "{0} セルは規則に合わないため、エラーの印を付けました",
        PasteRejected = "{0} セルは規則に合わないため、貼り付けませんでした",
        PasteTruncatedRows = "シートからはみ出した分({0} 行)は貼り付けませんでした",
        PasteTruncatedColumns = "シートからはみ出した分({0} 列)は貼り付けませんでした",
        PasteTruncatedBoth = "シートからはみ出した分({0} 行・{1} 列)は貼り付けませんでした",
        MessageSeparator = "。",
        MenuCut = "切り取り",
        MenuCopy = "コピー",
        MenuPaste = "貼り付け",
        MenuInsertRowsAbove = "上に {0} 行挿入",
        MenuInsertRowsBelow = "下に {0} 行挿入",
        MenuDeleteRows = "{0} 行を削除",
        MenuInsertColumnsLeft = "左に {0} 列挿入",
        MenuInsertColumnsRight = "右に {0} 列挿入",
        MenuDeleteColumns = "{0} 列を削除",
        MenuPasteFailed = "メニューからは貼り付けられませんでした。Ctrl+V で貼り付けてください",
        CalendarToday = "今日",
        CalendarPrevious = "前の月",
        CalendarNext = "次の月",
        CellInput = "セルの入力"
    };

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

    public string Fullwidth { get; init; } = "Enter full-width characters.";

    public string Halfwidth { get; init; } = "Enter half-width characters.";

    public string Hiragana { get; init; } = "Enter hiragana.";

    public string Katakana { get; init; } = "Enter full-width katakana.";

    public string HalfwidthKatakana { get; init; } = "Enter half-width katakana.";

    public string Alphanumeric { get; init; } = "Enter half-width letters and digits.";

    public string Digits { get; init; } = "Enter half-width digits.";

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
