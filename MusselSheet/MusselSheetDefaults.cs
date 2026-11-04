namespace MusselSheet;

public sealed class MusselSheetDefaults
{
    public SheetMoveDirection? MoveAfterReturnDirection { get; set; }

    public SheetReadyEnterMode? ReadyEnterMode { get; set; }

    public bool? TabReturnEnabled { get; set; }

    public SheetRowEndMode? RowEndMode { get; set; }

    public SheetSkipReadOnlyMode? SkipReadOnlyMode { get; set; }

    public SheetLastRowEnterMode? LastRowEnterMode { get; set; }

    public SheetCommitMode? CommitMode { get; set; }

    public SheetValidationMode? ValidationMode { get; set; }

    public int? UndoLimit { get; set; }

    public bool? ShowRowHeaders { get; set; }

    public bool? ShowColumnHeaders { get; set; }

    public bool? ShowGridLines { get; set; }

    public bool? ShowChanges { get; set; }

    public bool? AllowInsertRows { get; set; }

    public bool? AllowDeleteRows { get; set; }

    public bool? AllowInsertColumns { get; set; }

    public bool? AllowDeleteColumns { get; set; }

    public bool? FillHandleEnabled { get; set; }
}
