namespace MusselSheet;

using System.Globalization;
using System.Text;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MusselSheet.Internal;

public sealed partial class MusselSheetView : IAsyncDisposable
{
    private const string ModulePath = "./_content/MusselSheet/MusselSheet.js";

    private const int DefaultViewportHeight = 600;

    private const int DefaultViewportWidth = 1200;

    private const int MaxSuggestions = 50;

    private const long MaxPasteBytes = 16 * 1024 * 1024;

    private static int instanceCount;

    private readonly AxisLayout rowLayout = new();

    private readonly AxisLayout columnLayout = new();

    private readonly SheetStyles styles = new($"mussel-sheet-{Interlocked.Increment(ref instanceCount)}");

    private readonly EditHistory history = new();

    private Sheet? subscribed;

    private int layoutVersion = -1;

    private string? viewSignature;

    private int contentWidth;

    private int firstRow;

    private int lastRow = -1;

    private int scrollTop;

    private int viewportHeight;

    private int firstColumn;

    private int lastColumn = -1;

    private int scrollLeft;

    private int viewportWidth;

    private ElementReference root;

    private IJSObjectReference? module;

    private IJSObjectReference? controller;

    private Task? attaching;

    private DotNetObjectReference<SheetScriptBridge>? self;

    private (int Top, int Bottom, int Left, int Right)? sentThresholds;

    private string? sentMap;

    private string? sentOptions;

    private bool defaultsApplied;

    // Browser UTC offset in minutes (JS getTimezoneOffset), so a date typed without a year uses the user's date
    // even when running on Server.
    private int? clientOffset;

    private CancellationTokenSource? suggestionCancel;

    private CellEditContext? customEdit;

    private bool customPending;

    private ElementReference customElement;

    private bool disposed;

    [Parameter]
    [EditorRequired]
    public Sheet Sheet { get; set; } = default!;

    [Parameter]
    public string Width { get; set; } = "auto";

    [Parameter]
    public string Height { get; set; } = "auto";

    [Parameter]
    public bool ShowRowHeaders { get; set; } = true;

    [Parameter]
    public bool ShowColumnHeaders { get; set; } = true;

    [Parameter]
    public bool ShowGridLines { get; set; } = true;

    [Parameter]
    public bool ShowChanges { get; set; }

    [Parameter]
    public int RowHeaderWidth { get; set; } = 40;

    [Parameter]
    public int? ColumnHeaderHeight { get; set; }

    [Parameter]
    public int OverscanCount { get; set; } = 10;

    [Parameter]
    public int OverscanColumnCount { get; set; } = 3;

    [Parameter]
    public int? FrozenRows { get; set; }

    [Parameter]
    public int? FrozenColumns { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Parameter]
    public SheetMoveDirection MoveAfterReturnDirection { get; set; } = SheetMoveDirection.Down;

    [Parameter]
    public SheetReadyEnterMode ReadyEnterMode { get; set; } = SheetReadyEnterMode.Move;

    [Parameter]
    public bool TabReturnEnabled { get; set; } = true;

    [Parameter]
    public SheetRowEndMode RowEndMode { get; set; } = SheetRowEndMode.NextRow;

    [Parameter]
    public SheetSkipReadOnlyMode SkipReadOnlyMode { get; set; } = SheetSkipReadOnlyMode.EnterAndTab;

    [Parameter]
    public SheetLastRowEnterMode LastRowEnterMode { get; set; } = SheetLastRowEnterMode.Stop;

    [Parameter]
    public bool AllowInsertRows { get; set; }

    [Parameter]
    public bool AllowDeleteRows { get; set; }

    [Parameter]
    public bool AllowInsertColumns { get; set; }

    [Parameter]
    public bool AllowDeleteColumns { get; set; }

    [Parameter]
    public bool FillHandleEnabled { get; set; } = true;

    [Parameter]
    public SheetCommitMode CommitMode { get; set; } = SheetCommitMode.Optimistic;

    [Parameter]
    public SheetValidationMode ValidationMode { get; set; } = SheetValidationMode.Stay;

    [Parameter]
    public int UndoLimit { get; set; } = 100;

    [Parameter]
    public RenderFragment<CellEditContext>? CustomEditor { get; set; }

    [Parameter]
    public RenderFragment<CellDisplayContext>? CustomDisplay { get; set; }

    [Parameter]
    public EventCallback<CellCommittingEventArgs> CellCommitting { get; set; }

    [Parameter]
    public EventCallback<CellCommittedEventArgs> CellCommitted { get; set; }

    [Parameter]
    public EventCallback<SheetRowsInsertingEventArgs> RowsInserting { get; set; }

    [Parameter]
    public EventCallback<SheetRowsDeletingEventArgs> RowsDeleting { get; set; }

    [Parameter]
    public EventCallback<SheetColumnsInsertingEventArgs> ColumnsInserting { get; set; }

    [Parameter]
    public EventCallback<SheetColumnsDeletingEventArgs> ColumnsDeleting { get; set; }

    [Parameter]
    public EventCallback<SheetPastingEventArgs> Pasting { get; set; }

    [Parameter]
    public EventCallback<SheetFillingEventArgs> Filling { get; set; }

    [Parameter]
    public EventCallback<CellAddress> ActiveCellChanged { get; set; }

    [Parameter]
    public EventCallback<CellRange> SelectionChanged { get; set; }

    [Parameter]
    public EventCallback<SheetMenuItemClickedEventArgs> MenuItemClicked { get; set; }

    public CellAddress ActiveCell { get; private set; }

    public CellRange Selection { get; private set; }

    public bool CanUndo => history.CanUndo;

    public bool CanRedo => history.CanRedo;

    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    internal DateOnly Today => clientOffset is { } offset
        ? DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime.AddMinutes(-offset))
        : DateOnly.FromDateTime(Clock.GetLocalNow().DateTime);

    internal int ViewVersion { get; private set; }

    internal int FrozenRowCount => Math.Clamp(FrozenRows ?? Sheet.FrozenRows, 0, Sheet.RowCount);

    internal int FrozenColumnCount => Math.Clamp(FrozenColumns ?? Sheet.FrozenColumns, 0, Sheet.ColumnCount);

    internal int RestFirst => Math.Max(firstColumn, FrozenColumnCount);

    internal int RestLast => Math.Min(lastColumn, Sheet.ColumnCount - 1);

    internal int ColumnVersion { get; private set; }

    [Inject]
    private IJSRuntime Runtime { get; set; } = default!;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    private MusselSheetOptions Options
    {
        get => field ??= Services.GetService(typeof(MusselSheetOptions)) as MusselSheetOptions ?? MusselSheetOptions.Default;
        set;
    }

    internal SheetTexts Texts => Options.TextsFor(CultureInfo.CurrentUICulture);

    internal string DateFormat => Options.DateFormatFor(Sheet.Culture);

    internal InputContext InputContext => new(Sheet.Culture, Today, Texts, DateFormat, ReadingRules.From(Sheet.Culture, Options.JapaneseDatesFor(Sheet.Culture))) { Eras = Eras };

    internal IReadOnlyList<SheetJapaneseEra> Eras => Options.Eras as IReadOnlyList<SheetJapaneseEra> ?? [.. Options.Eras];

    private CommitResult Rejected => new(false, Texts.NotEditable, false);

    private bool IsVirtualized => !IsAuto(Height);

    private bool IsColumnVirtualized => !IsAuto(Width);

    private int HeaderHeight => ShowColumnHeaders ? (ColumnHeaderHeight ?? Sheet.DefaultRowHeight) : 0;

    private int HeaderWidth => ShowRowHeaders ? RowHeaderWidth : 0;

    private string RootClass
    {
        get
        {
            var builder = new StringBuilder("mussel-sheet ").Append(styles.Scope);
            if (!ShowGridLines)
            {
                builder.Append(" mussel-sheet-nogrid");
            }

            if (IsAuto(Width) && IsAuto(Height))
            {
                builder.Append(" mussel-sheet-autosize");
            }

            if (FrozenRowCount > 0)
            {
                builder.Append(" mussel-sheet-frozen-rows");
            }

            if (AdditionalAttributes?.GetValueOrDefault("class") is { } extra)
            {
                builder.Append(' ').Append(extra);
            }

            return builder.ToString();
        }
    }

    private string RootStyle => string.Create(CultureInfo.InvariantCulture, $"width:{(IsAuto(Width) ? "max-content" : Width)};height:{(IsAuto(Height) ? "auto" : Height)}");

    private string ContentStyle => string.Create(CultureInfo.InvariantCulture, $"width:{contentWidth}px");

    private string ColumnHeaderStyle => RowStyle(HeaderHeight);

    private int BodyFirst => Math.Max(firstRow, FrozenRowCount);

    private int TopSpace => lastRow >= BodyFirst ? rowLayout.Start(BodyFirst) - rowLayout.Start(FrozenRowCount) : 0;

    private int BottomSpace => lastRow >= BodyFirst ? rowLayout.Total - rowLayout.End(lastRow) : rowLayout.Total - rowLayout.Start(FrozenRowCount);

    // Counts the header row and column, and every row including those not rendered because of virtualization
    private int AriaRowCount => Sheet.RowCount + (ShowColumnHeaders ? 1 : 0);

    private int AriaColumnCount => Sheet.ColumnCount + (ShowRowHeaders ? 1 : 0);

    public Task FocusAsync() => InvokeScriptAsync("focus");

    public Task SetActiveCellAsync(int row, int column) => InvokeScriptAsync("setActiveCell", row, column);

    public Task BeginEditAsync() => InvokeScriptAsync("beginEdit");

    public Task CommitEditAsync() => InvokeScriptAsync("commitEdit");

    public Task CancelEditAsync() => InvokeScriptAsync("cancelEdit");

    public async Task<bool> UndoAsync()
    {
        var target = await ApplyHistoryAsync(false).ConfigureAwait(true);
        if (target is { } range)
        {
            await InvokeScriptAsync("setSelection", range.Top, range.Left, range.Bottom, range.Right).ConfigureAwait(true);
        }

        return target is not null;
    }

    public async Task<bool> RedoAsync()
    {
        var target = await ApplyHistoryAsync(true).ConfigureAwait(true);
        if (target is { } range)
        {
            await InvokeScriptAsync("setSelection", range.Top, range.Left, range.Bottom, range.Right).ConfigureAwait(true);
        }

        return target is not null;
    }

    internal Task OnMenuItemAsync(string name, int top, int left, int bottom, int right, int row, int column) =>
        MenuItemClicked.InvokeAsync(new SheetMenuItemClickedEventArgs(Sheet, name, new CellRange(top, left, bottom, right), new CellAddress(row, column)));

    internal async Task OnActiveCellChangedAsync(int row, int column, int top, int left, int bottom, int right)
    {
        var address = new CellAddress(row, column);
        var selection = new CellRange(top, left, bottom, right);
        var moved = address != ActiveCell;
        var selected = selection != Selection;
        ActiveCell = address;
        Selection = selection;
        if (moved)
        {
            await ActiveCellChanged.InvokeAsync(address).ConfigureAwait(true);
        }

        if (selected)
        {
            await SelectionChanged.InvokeAsync(selection).ConfigureAwait(true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        // Cancel a pending suggestion query; GetSuggestionsAsync disposes the same source too, which is harmless
        if (suggestionCancel is not null)
        {
            await suggestionCancel.CancelAsync().ConfigureAwait(true);
        }

        suggestionCancel?.Dispose();
        suggestionCancel = null;

        Unsubscribe();
        await DetachAsync().ConfigureAwait(true);
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
            }

            module = null;
        }

        self?.Dispose();
        self = null;
    }

    // Registered defaults are applied first and the parameters set on the sheet after, so the sheet's own values win
    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (!defaultsApplied)
        {
            defaultsApplied = true;
            ApplyDefaults(Options.Defaults);
        }

        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        if (Sheet is null)
        {
            throw new InvalidOperationException($"{nameof(MusselSheetView)} requires the {nameof(Sheet)} parameter.");
        }

        if (!ReferenceEquals(subscribed, Sheet))
        {
            Unsubscribe();
            Sheet.Changed += OnSheetChanged;
            subscribed = Sheet;
            layoutVersion = -1;
            history.Clear();
        }

        history.Limit = UndoLimit;
        if ((customEdit is not null) && (CustomEditor is null))
        {
            customEdit = null;
        }

        CheckView();
        Refresh();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            if (controller is null)
            {
                attaching ??= AttachAsync();
                await attaching.ConfigureAwait(true);
                attaching = null;
            }

            if (controller is null)
            {
                return;
            }

            await SyncMapAsync().ConfigureAwait(true);
            var options = ScriptOptions();
            var optionsText = OptionsSignature(options);
            if (!string.Equals(sentOptions, optionsText, StringComparison.Ordinal))
            {
                sentOptions = optionsText;
                await controller.InvokeVoidAsync("setOptions", SheetScriptJson.ToElement(options)).ConfigureAwait(true);
            }

            var thresholds = Thresholds();
            if (sentThresholds != thresholds)
            {
                sentThresholds = thresholds;
                await controller.InvokeVoidAsync("setCovered", thresholds.Top, thresholds.Bottom, thresholds.Left, thresholds.Right).ConfigureAwait(true);
            }

            // A newly opened custom editor is shown by JS after the render, which positions it and moves focus to it
            if (customPending && (customEdit is not null))
            {
                customPending = false;
                await controller.InvokeVoidAsync("showCustom", customElement).ConfigureAwait(true);
            }
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private bool IsInSheet(int row, int column) =>
        (row >= 0) && (row < Sheet.RowCount) && (column >= 0) && (column < Sheet.ColumnCount);

    private bool IsEditable(int row, int column) =>
        IsInSheet(row, column) && !Sheet.GetSettings(row, column).IsInputDisabled && !Sheet.IsCovered(row, column);

    private void OnSheetChanged(object? sender, SheetChangedEventArgs e) =>
        _ = InvokeAsync(() =>
        {
            if (!disposed)
            {
                CheckView();
                Refresh();
                StateHasChanged();
            }
        });

    // Frozen row and column counts can also be changed on the Sheet, so sheet changes run this check too
    private void CheckView()
    {
        var signature = string.Create(CultureInfo.InvariantCulture, $"{ShowRowHeaders}|{ShowColumnHeaders}|{ShowChanges}|{RowHeaderWidth}|{ColumnHeaderHeight}|{Height}|{Width}|{OverscanCount}|{OverscanColumnCount}|{CustomDisplay is not null}|{FrozenRows ?? Sheet.FrozenRows}|{FrozenColumns ?? Sheet.FrozenColumns}");
        if (!string.Equals(signature, viewSignature, StringComparison.Ordinal))
        {
            viewSignature = signature;
            ViewVersion++;
            layoutVersion = -1;
        }
    }

    private void ApplyDefaults(MusselSheetDefaults defaults)
    {
        MoveAfterReturnDirection = defaults.MoveAfterReturnDirection ?? MoveAfterReturnDirection;
        ReadyEnterMode = defaults.ReadyEnterMode ?? ReadyEnterMode;
        TabReturnEnabled = defaults.TabReturnEnabled ?? TabReturnEnabled;
        RowEndMode = defaults.RowEndMode ?? RowEndMode;
        SkipReadOnlyMode = defaults.SkipReadOnlyMode ?? SkipReadOnlyMode;
        LastRowEnterMode = defaults.LastRowEnterMode ?? LastRowEnterMode;
        CommitMode = defaults.CommitMode ?? CommitMode;
        ValidationMode = defaults.ValidationMode ?? ValidationMode;
        UndoLimit = defaults.UndoLimit ?? UndoLimit;
        ShowRowHeaders = defaults.ShowRowHeaders ?? ShowRowHeaders;
        ShowColumnHeaders = defaults.ShowColumnHeaders ?? ShowColumnHeaders;
        ShowGridLines = defaults.ShowGridLines ?? ShowGridLines;
        ShowChanges = defaults.ShowChanges ?? ShowChanges;
        AllowInsertRows = defaults.AllowInsertRows ?? AllowInsertRows;
        AllowDeleteRows = defaults.AllowDeleteRows ?? AllowDeleteRows;
        AllowInsertColumns = defaults.AllowInsertColumns ?? AllowInsertColumns;
        AllowDeleteColumns = defaults.AllowDeleteColumns ?? AllowDeleteColumns;
        FillHandleEnabled = defaults.FillHandleEnabled ?? FillHandleEnabled;
    }

    private void Unsubscribe()
    {
        if (subscribed is not null)
        {
            subscribed.Changed -= OnSheetChanged;
            subscribed = null;
        }
    }
}
