namespace MusselSheet;

using MusselSheet.Internal;

public sealed class EditHistoryTests
{
    [Fact]
    public void UndoAndRedoMoveEntriesBetweenTheStacks()
    {
        var history = new EditHistory();
        var first = Entry();
        var second = Entry();
        history.Record(first);
        history.Record(second);

        Assert.Same(second, history.TakeUndo());
        Assert.True(history.CanRedo);
        Assert.Same(second, history.TakeRedo());
        Assert.Same(second, history.TakeUndo());
        Assert.Same(first, history.TakeUndo());
        Assert.Null(history.TakeUndo());
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void RecordingClearsRedo()
    {
        var history = new EditHistory();
        history.Record(Entry());
        history.TakeUndo();

        history.Record(Entry());

        Assert.False(history.CanRedo);
        Assert.Null(history.TakeRedo());
    }

    [Fact]
    public void OldestEntriesAreDroppedOverTheLimit()
    {
        var history = new EditHistory { Limit = 2 };
        EditEntry[] entries = [Entry(), Entry(), Entry()];
        foreach (var entry in entries)
        {
            history.Record(entry);
        }

        Assert.Same(entries[2], history.TakeUndo());
        Assert.Same(entries[1], history.TakeUndo());
        Assert.Null(history.TakeUndo());
    }

    private static CellsEdited Entry() => new([]);
}
