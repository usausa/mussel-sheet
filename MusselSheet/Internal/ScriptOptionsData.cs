namespace MusselSheet.Internal;

internal sealed record ScriptOptionsData(
    string EnterDirection,
    string ReadyEnter,
    bool TabReturn,
    string RowEnd,
    string SkipReadOnly,
    string LastRowEnter,
    string CommitMode,
    bool Autosize,
    bool CustomEditor,
    bool AllowInsertRows,
    bool AllowDeleteRows,
    bool AllowInsertColumns,
    bool AllowDeleteColumns,
    bool AllowFill,
    int FrozenRows,
    int FrozenColumns,
    IReadOnlyDictionary<string, string?> ReadyKeys,
    IReadOnlyDictionary<string, string?> EditingKeys,
    IReadOnlyList<string> HiddenMenuItems,
    IReadOnlyList<ScriptItem> MenuItems);
