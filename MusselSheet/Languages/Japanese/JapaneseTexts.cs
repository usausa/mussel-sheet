namespace MusselSheet.Languages.Japanese;

public static class JapaneseTexts
{
    public static SheetTexts Default { get; } = new()
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
        Characters = "入力できない文字が含まれています",
        CharacterSets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fullwidth"] = "全角で入力してください",
            ["halfwidth"] = "半角で入力してください",
            ["hiragana"] = "ひらがなで入力してください",
            ["katakana"] = "全角カタカナで入力してください",
            ["halfwidthKatakana"] = "半角カタカナで入力してください",
            ["alphanumeric"] = "半角英数字で入力してください",
            ["digits"] = "半角数字で入力してください"
        },
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
}
