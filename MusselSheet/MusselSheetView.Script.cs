namespace MusselSheet;

using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.JSInterop;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    private string MapSignature() =>
        string.Create(CultureInfo.InvariantCulture, $"{RuntimeHelpers.GetHashCode(Sheet)}|{Sheet.StructureVersion}|{HeaderHeight}|{HeaderWidth}|{ValidationMode}");

    private SheetMap BuildMap() =>
        SheetMapBuilder.Build(Sheet, HeaderHeight, HeaderWidth, ValidationMode, InputContext, CalendarInfo.From(Sheet.Culture, Options.FirstDayOfWeekFor(Sheet.Culture)));

    // Resend the sheet map only when the shape or settings changed. After a user-driven row change,
    // send it without waiting for the render to finish, so it arrives before JS moves to that row.
    private async Task SyncMapAsync()
    {
        var map = MapSignature();
        if ((controller is null) || string.Equals(sentMap, map, StringComparison.Ordinal))
        {
            return;
        }

        sentMap = map;
        try
        {
            await controller.InvokeVoidAsync("setMap", SheetScriptJson.ToElement(BuildMap())).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private ScriptOptionsData ScriptOptions()
    {
        var enterDirection = MoveAfterReturnDirection switch
        {
            SheetMoveDirection.Right => "right",
            SheetMoveDirection.None => "none",
            _ => "down"
        };
        var rowEnd = RowEndMode switch
        {
            SheetRowEndMode.Stop => "stop",
            SheetRowEndMode.LeaveSheet => "leave",
            _ => "next"
        };
        var skip = SkipReadOnlyMode switch
        {
            SheetSkipReadOnlyMode.None => "none",
            SheetSkipReadOnlyMode.All => "all",
            _ => "enterTab"
        };
        return new ScriptOptionsData(
            enterDirection,
            ReadyEnterMode == SheetReadyEnterMode.Edit ? "edit" : "move",
            TabReturnEnabled,
            rowEnd,
            skip,
            LastRowEnterMode == SheetLastRowEnterMode.AddRow ? "add" : "stop",
            CommitMode == SheetCommitMode.Wait ? "wait" : "optimistic",
            IsAuto(Width) && IsAuto(Height),
            CustomEditor is not null,
            AllowInsertRows,
            AllowDeleteRows,
            AllowInsertColumns,
            AllowDeleteColumns,
            FillHandleEnabled,
            FrozenRowCount,
            FrozenColumnCount,
            KeyCombo.NormalizeAll(Options.ReadyKeys),
            KeyCombo.NormalizeAll(Options.EditingKeys),
            [.. Options.Menu.Hidden],
            [.. Options.Menu.Items.Select(static x => new ScriptItem(x.Name, x.TextFor(CultureInfo.CurrentUICulture)))]);
    }

    private async Task AttachAsync()
    {
        module ??= await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
        self ??= DotNetObjectReference.Create(new SheetScriptBridge(this));
        var options = SheetScriptJson.ToElement(ScriptOptions());
        sentMap = MapSignature();
        sentOptions = options.GetRawText();
        sentThresholds = null;
        controller = await module.InvokeAsync<IJSObjectReference>("attach", root, self, options, SheetScriptJson.ToElement(BuildMap())).ConfigureAwait(true);
        clientOffset = await controller.InvokeAsync<int>("timezoneOffset").ConfigureAwait(true);
    }

    private async Task DetachAsync()
    {
        if (controller is null)
        {
            return;
        }

        var current = controller;
        controller = null;
        sentThresholds = null;
        try
        {
            await current.InvokeVoidAsync("dispose").ConfigureAwait(true);
            await current.DisposeAsync().ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task InvokeScriptAsync(string identifier, params object?[] args)
    {
        if (controller is null)
        {
            return;
        }

        try
        {
            await controller.InvokeVoidAsync(identifier, args).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
