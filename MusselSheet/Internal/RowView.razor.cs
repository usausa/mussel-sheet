namespace MusselSheet.Internal;

using Microsoft.AspNetCore.Components;

public sealed partial class RowView
{
    private SheetRow? renderedRow;

    private int renderedIndex = -1;

    private int renderedStructure = -1;

    private int renderedContent = -1;

    private int renderedView = -1;

    private int renderedColumns = -1;

    [Parameter]
    [EditorRequired]
    public MusselSheetView Owner { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public SheetRow Row { get; set; } = default!;

    private string RowStyle => MusselSheetView.RowStyle(Row.ActualHeight);

    private int Frozen => Math.Min(Owner.FrozenColumnCount, Row.Sheet.ColumnCount);

    protected override bool ShouldRender() =>
        !ReferenceEquals(Row, renderedRow)
        || (Row.Index != renderedIndex)
        || (Row.Sheet.StructureVersion != renderedStructure)
        || (Row.ContentVersion != renderedContent)
        || (Owner.ViewVersion != renderedView)
        || (Owner.ColumnVersion != renderedColumns);

    private void Remember()
    {
        renderedRow = Row;
        renderedIndex = Row.Index;
        renderedStructure = Row.Sheet.StructureVersion;
        renderedContent = Row.ContentVersion;
        renderedView = Owner.ViewVersion;
        renderedColumns = Owner.ColumnVersion;
    }
}
