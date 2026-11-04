namespace MusselSheet;

using System.Text.Json;

using Bunit;

using Microsoft.AspNetCore.Components;

public sealed class ViewMethodTests : BunitContext
{
    public ViewMethodTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task MethodsCallTheScript()
    {
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, new Sheet(2, 2)));

        await cut.InvokeAsync(cut.Instance.FocusAsync).ConfigureAwait(true);
        await cut.InvokeAsync(() => cut.Instance.SetActiveCellAsync(1, 0)).ConfigureAwait(true);
        await cut.InvokeAsync(cut.Instance.BeginEditAsync).ConfigureAwait(true);
        await cut.InvokeAsync(cut.Instance.CommitEditAsync).ConfigureAwait(true);
        await cut.InvokeAsync(cut.Instance.CancelEditAsync).ConfigureAwait(true);

        JSInterop.VerifyInvoke("focus");
        object[] address = [1, 0];
        Assert.Equal(address, JSInterop.VerifyInvoke("setActiveCell").Arguments);
        JSInterop.VerifyInvoke("beginEdit");
        JSInterop.VerifyInvoke("commitEdit");
        JSInterop.VerifyInvoke("cancelEdit");
    }

    [Theory]
    [InlineData(SheetMoveDirection.Down, SheetRowEndMode.NextRow, SheetSkipReadOnlyMode.EnterAndTab, "down", "next", "enterTab")]
    [InlineData(SheetMoveDirection.Right, SheetRowEndMode.Stop, SheetSkipReadOnlyMode.None, "right", "stop", "none")]
    [InlineData(SheetMoveDirection.None, SheetRowEndMode.LeaveSheet, SheetSkipReadOnlyMode.All, "none", "leave", "all")]
    public void ModesReachTheScriptInItsOwnWords(SheetMoveDirection direction, SheetRowEndMode rowEnd, SheetSkipReadOnlyMode skip, string expectedDirection, string expectedRowEnd, string expectedSkip)
    {
        Render<MusselSheetView>(p => p
            .Add(x => x.Sheet, new Sheet(1, 1))
            .Add(x => x.MoveAfterReturnDirection, direction)
            .Add(x => x.RowEndMode, rowEnd)
            .Add(x => x.SkipReadOnlyMode, skip));

        var options = (JsonElement)JSInterop.Invocations["attach"].Single().Arguments[2]!;

        Assert.Equal(expectedDirection, options.GetProperty("enterDirection").GetString());
        Assert.Equal(expectedRowEnd, options.GetProperty("rowEnd").GetString());
        Assert.Equal(expectedSkip, options.GetProperty("skipReadOnly").GetString());
    }

    [Fact]
    public async Task EditorContextCommitsItsValueOrCancels()
    {
        var sheet = new Sheet(1, 1);
        sheet["A1"].Configure(static c => c.Kind = CellKind.Custom);
        CellEditContext? opened = null;
        var cut = Render<MusselSheetView>(p => p.Add(x => x.Sheet, sheet).Add(x => x.CustomEditor, Editor));

        await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 0)).ConfigureAwait(true);
        opened!.Value = "x";
        Assert.True(await cut.InvokeAsync(opened.CommitAsync).ConfigureAwait(true));
        Assert.Equal("x", sheet.GetValue(0, 0));
        Assert.Empty(cut.FindAll(".mussel-sheet-custom"));

        await cut.InvokeAsync(() => cut.Instance.OpenCustomEditor(0, 0)).ConfigureAwait(true);
        opened.Value = "y";
        await cut.InvokeAsync(opened.CancelAsync).ConfigureAwait(true);
        Assert.Equal("x", sheet.GetValue(0, 0));
        Assert.Empty(cut.FindAll(".mussel-sheet-custom"));

        RenderFragment Editor(CellEditContext context)
        {
            opened = context;
            return static builder => builder.AddContent(0, "editor");
        }
    }

    [Fact]
    public void TheSheetIsRequired()
    {
        Assert.Throws<InvalidOperationException>(() => Render<MusselSheetView>());
    }
}
