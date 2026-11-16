namespace MusselSheet;

using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.JSInterop;

using MusselSheet.Internal;

public sealed partial class MusselSheetView
{
    internal async Task ReportAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            if (!disposed)
            {
                await DispatchExceptionAsync(ex).ConfigureAwait(true);
            }

            throw;
        }
    }

    internal async Task<T> ReportAsync<T>(Task<T> task)
    {
        try
        {
            return await task.ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not (JSDisconnectedException or OperationCanceledException))
        {
            if (!disposed)
            {
                await DispatchExceptionAsync(ex).ConfigureAwait(true);
            }

            throw;
        }
    }

    private string MapSignature() =>
        string.Create(CultureInfo.InvariantCulture, $"{RuntimeHelpers.GetHashCode(Sheet)}|{Sheet.StructureVersion}|{HeaderHeight}|{HeaderWidth}|{ValidationMode}");

    private string LanguageSignature() =>
        string.Create(CultureInfo.InvariantCulture, $"{RuntimeHelpers.GetHashCode(Options)}|{RuntimeHelpers.GetHashCode(Sheet.Culture)}|{CultureInfo.CurrentUICulture.Name}|{Today}");

    private SheetMap BuildMap() => SheetMapBuilder.Build(Sheet, HeaderHeight, HeaderWidth, ValidationMode, InputContext);

    private ScriptLanguage BuildLanguage() =>
        SheetMapBuilder.Language(InputContext, CalendarInfo.From(Sheet.Culture, Options.FirstDayOfWeekFor(Sheet.Culture)));

    // Sends the map only when it changed; after a user's row change, without waiting for the render
    private async Task SyncMapAsync()
    {
        if (controller is not { } current)
        {
            return;
        }

        try
        {
            var language = LanguageSignature();
            if (!string.Equals(sentLanguage, language, StringComparison.Ordinal))
            {
                sentLanguage = language;
                await current.InvokeVoidAsync("setLanguage", SheetScriptJson.ToElement(BuildLanguage())).ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }
            }

            var map = MapSignature();
            if (!string.Equals(sentMap, map, StringComparison.Ordinal))
            {
                sentMap = map;
                await current.InvokeVoidAsync("setMap", SheetScriptJson.ToElement(BuildMap())).ConfigureAwait(true);
            }
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
        try
        {
            module ??= await Runtime.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            if (disposed)
            {
                await ReleaseModuleAsync().ConfigureAwait(true);
                return;
            }

            self ??= DotNetObjectReference.Create(new SheetScriptBridge(this));
            var options = SheetScriptJson.ToElement(ScriptOptions());
            sentMap = MapSignature();
            sentLanguage = LanguageSignature();
            sentOptions = options.GetRawText();
            sentThresholds = null;
            controller = await module.InvokeAsync<IJSObjectReference>("attach", root, self, options, SheetScriptJson.ToElement(BuildMap()), SheetScriptJson.ToElement(BuildLanguage())).ConfigureAwait(true);
            if (disposed)
            {
                await DetachAsync().ConfigureAwait(true);
                return;
            }

            clientOffset = await controller.InvokeAsync<int>("timezoneOffset").ConfigureAwait(true);
            while (!disposed && pendingCalls.TryDequeue(out var call))
            {
                await controller.InvokeVoidAsync(call.Identifier, call.Args).ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task ReleaseModuleAsync()
    {
        var loaded = module;
        module = null;
        if (loaded is null)
        {
            return;
        }

        try
        {
            await loaded.DisposeAsync().ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
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
        catch (JSException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task InvokeScriptAsync(string identifier, params object?[] args)
    {
        if ((controller is null) || (pendingCalls.Count > 0))
        {
            pendingCalls.Enqueue((identifier, args));
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
