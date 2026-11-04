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
    ScriptKeysData Keys,
    ScriptMenuData Menu);

internal sealed record ScriptKeysData(IReadOnlyDictionary<string, string?> Ready, IReadOnlyDictionary<string, string?> Editing);

internal sealed record ScriptMenuData(IReadOnlyList<string> Hidden, IReadOnlyList<ScriptMenuItemData> Items);

internal sealed record ScriptMenuItemData(string Name, string Text);
