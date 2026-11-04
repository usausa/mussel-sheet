# MusselSheet API reference

## 📋 MusselSheetView

The sheet component. `Sheet` is required, and other attributes such as `class` and `id` go to the root element.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Sheet` | `Sheet` | required | The sheet to show and edit |
| `Width`, `Height` | `string` | `auto` | CSS lengths; `auto` follows the content, a fixed size scrolls inside and draws only the visible rows and columns |
| `ShowRowHeaders`, `ShowColumnHeaders`, `ShowGridLines` | `bool` | `true` | Headers and grid lines |
| `ShowChanges` | `bool` | `false` | Marks cells the user changed from their original value |
| `RowHeaderWidth`, `ColumnHeaderHeight` | `int`, `int?` | `40`, `null` | Header sizes in pixels; `null` follows the default row height |
| `OverscanCount`, `OverscanColumnCount` | `int` | `10`, `3` | Extra rows and columns drawn around the visible ones |
| `FrozenRows`, `FrozenColumns` | `int?` | `null` | Overrides `Sheet.FrozenRows` and `Sheet.FrozenColumns` |
| `MoveAfterReturnDirection` | `SheetMoveDirection` | `Down` | `Down`, `Right` or `None` after Enter |
| `ReadyEnterMode` | `SheetReadyEnterMode` | `Move` | Enter on a selected cell moves (`Move`) or starts editing (`Edit`) |
| `TabReturnEnabled` | `bool` | `true` | Enter after Tab returns to the column where Tab started |
| `RowEndMode` | `SheetRowEndMode` | `NextRow` | At the last input cell of a row: `NextRow`, `Stop` or `LeaveSheet` (Tab only) |
| `SkipReadOnlyMode` | `SheetSkipReadOnlyMode` | `EnterAndTab` | Which keys skip input-disabled cells: `None`, `EnterAndTab` or `All` |
| `LastRowEnterMode` | `SheetLastRowEnterMode` | `Stop` | `AddRow` adds a row below when Enter is pressed on the last input cell |
| `AllowInsertRows`, `AllowDeleteRows`, `AllowInsertColumns`, `AllowDeleteColumns` | `bool` | `false` | Commands of the context menu |
| `FillHandleEnabled` | `bool` | `true` | Fill handle of the selection |
| `CommitMode` | `SheetCommitMode` | `Optimistic` | `Optimistic` moves without waiting for C#; `Wait` moves after C# accepted the value |
| `ValidationMode` | `SheetValidationMode` | `Stay` | On a validation error: `Stay` in the cell, `Revert`, or `AcceptAndMark` |
| `UndoLimit` | `int` | `100` | Number of undo steps |
| `CustomEditor`, `CustomDisplay` | `RenderFragment<CellEditContext>?`, `RenderFragment<CellDisplayContext>?` | `null` | Templates of `CellKind.Custom` cells; `CellSettings.Editor` tells them apart |

### Events

| Name | Arguments | Description |
|---|---|---|
| `CellCommitting` | `CellCommittingEventArgs` | Before a value is stored; can change `Value` and `Error`, or `Cancel` |
| `CellCommitted` | `CellCommittedEventArgs` | After a value is stored, with `OldValue` and `Reason` |
| `RowsInserting`, `RowsDeleting`, `ColumnsInserting`, `ColumnsDeleting` | `Sheet…EventArgs` | Before the user changes the structure; `Cancel` stops it |
| `Pasting`, `Filling` | `SheetPastingEventArgs`, `SheetFillingEventArgs` | Before a paste or a fill; `Cancel` stops it |
| `ActiveCellChanged`, `SelectionChanged` | `CellAddress`, `CellRange` | The active cell and the selection |
| `MenuItemClicked` | `SheetMenuItemClickedEventArgs` | An item registered in `MusselSheetOptions.Menu` |

`CellChangeReason` tells where a value came from: `Input`, `ClearContents`, `Toggle`, `Undo`, `Redo`, `Paste`, `Cut` or `Fill`.  

### Members

| Name | Description |
|---|---|
| `ActiveCell`, `Selection` | The active cell and the selected range |
| `CanUndo`, `CanRedo`, `UndoAsync()`, `RedoAsync()` | Undo and redo |
| `FocusAsync()`, `SetActiveCellAsync(row, column)` | Focus and move |
| `BeginEditAsync()`, `CommitEditAsync()`, `CancelEditAsync()` | Editing |

### Templates

| Name | Context |
|---|---|
| `CustomEditor` | `CellEditContext`: `Sheet`, `Address`, `Name`, `Value`, `Error`, `CommitAsync()`, `CommitAsync(value)` and `CancelAsync()` |
| `CustomDisplay` | `CellDisplayContext`: `Sheet`, `Address`, `Name`, `Value` and `Text` |

## 🧾 Sheet

The model of the sheet. Rows and columns are numbered from 0; addresses use the A1 style (`B2`, `B2:D5`, `B:D`, `2:4`).  

| Name | Description |
|---|---|
| `new Sheet(rows, columns)` | Creates a sheet |
| `RowCount`, `ColumnCount`, `Rows`, `Columns` | Size, and `SheetRow` / `SheetColumn` with `Height` / `Width` and `Header` |
| `DefaultRowHeight`, `DefaultColumnWidth` | Default sizes in pixels |
| `FrozenRows`, `FrozenColumns` | Rows from the top and columns from the left that stay visible |
| `Culture` | How text is shown and read; the current culture by default |
| `this[row, column]`, `this[address]` | A `Cell` with `Value`, `Text`, `EditText`, `InputText`, `Error`, `Settings` and `GetValue<T>()` |
| `Range(address)`, `Cells` | A `SheetRange`, or all cells |
| `Configure(range, settings)`, `GetSettings(row, column)` | Adds cell settings; later settings win item by item |
| `GetValue(row, column)`, `GetValue<T>`, `TryGetValue<T>`, `SetValue` | One value |
| `GetValues(range)`, `SetValues(top, left, values)`, `ClearContents(range)` | Many values; settings stay |
| `GetText`, `GetEditText`, `GetInputText` | The shown text, the text F2 starts with, and the text as typed |
| `GetError`, `SetError` | The error mark of a cell |
| `Merge(range)`, `Unmerge(range)`, `MergeAreas`, `GetMergeArea(row, column)` | Merged cells; only the top left value stays |
| `AddRows`, `InsertRows`, `DeleteRows`, `AddColumns`, `InsertColumns`, `DeleteColumns` | Structure |
| `IsChanged`, `GetOriginalValue`, `GetChangedCells()`, `AcceptChanges()` | Cells the user changed |
| `Changed`, `BeginUpdate()` | Change notification with `Kind` (`SheetChangeKind`), `Range`, `Index` and `Count`, and grouping changes into one |
| `Bind(source)` | Binds cells to an object; see Binding |
| `Compute`, `ComputeRows` | Computed cells; see Computed cells |
| `ToDesign`, `FromDesign` | Saves and loads the design; see Design |

`SheetRange` has `Configure`, `GetValues`, `SetValues`, `ClearContents`, `Merge`, `Unmerge` and `BorderAround`.  
`SheetRow` and `SheetColumn` also have `Index`, `ActualHeight` / `ActualWidth` and `HeaderText`. `Cell` also has `Address`, `IsChanged`, `OriginalValue`, `TryGetValue<T>`, `Configure` and `ClearContents`.  

## 📍 Addresses

| Type | Members |
|---|---|
| `CellAddress` | `Row`, `Column`, `Parse`, `TryParse`, `ColumnName`, `TryParseColumn`, `TryParseRow` |
| `CellRange` | `Top`, `Left`, `Bottom`, `Right`, `All`, `Cell`, `Rows`, `Columns`, `Parse`, `TryParse`, `Contains`, `Intersects`, `TryClip`, `IsEntireColumns`, `IsEntireRows` |

## ⚙️ CellSettings

Everything is optional, and later settings win item by item. `Sheet.GetSettings` returns the result as `ResolvedCellSettings`.  

| Name | Description |
|---|---|
| `Kind` | `Text`, `Label`, `Number`, `Date`, `Time`, `Checkbox`, `List`, `Mask`, `AutoComplete` or `Custom` |
| `ReadOnly` | The cell can be selected and copied but not edited |
| `Format` | A .NET format string; dates also take the Japanese era symbols (`ggge年M月d日`) |
| `WrapText` | Alt+Enter adds a line break, and the text wraps in the cell |
| `MoveGroup` | Enter and Tab cycle inside cells of the same name |
| `Required`, `Minimum`, `Maximum`, `MaxLength`, `MaxByteLength`, `Pattern` | Input rules; the limits (`CellBound`) take a number, a `DateOnly` or a `TimeOnly` |
| `Items`, `SuggestionProvider` | The items of a list (`CellListItem` with `Value` and `Text`), and the candidates of an auto complete |
| `ErrorMessage`, `InputMessage` | The message of an error, and the note shown when the cell is selected |
| `ValidationMode` | Overrides the mode of the view |
| `Validator` | A function that returns an error message |
| `Corrector` | A function that corrects the value before the rules run |
| `Formatter`, `StyleProvider` | Functions that return the shown text and the style by value; they do not limit the input |
| `ImeMode` | `NoControl`, or `Off` to prefer direct input |
| `CharacterRestriction` | `Fullwidth`, `Halfwidth`, `Hiragana`, `Katakana`, `HalfwidthKatakana`, `Alphanumeric` or `Digits` |
| `CharacterConversions` | Flags: `FullwidthAsciiToHalfwidth`, `HalfwidthKatakanaToFullwidth`, `HiraganaToKatakana` and `KatakanaToHiragana` |
| `Mask` | The input mask of `CellKind.Mask` |
| `CheckedText`, `UncheckedText` | The texts of a checkbox shown as text |
| `Editor` | The name passed to `CustomEditor` |
| `Style` | A `CellStyle` |

| Function | Context | Returns |
|---|---|---|
| `Validator` | `CellValidationContext`: `Sheet`, `Address`, `Value`, `Text` | An error message, or `null` |
| `Corrector` | `CellCorrectionContext`: `Sheet`, `Address`, `Value`, `Text` | The value |
| `Formatter` | `CellFormatContext`: `Sheet`, `Address`, `Value`, `Text` | The shown text, or `null` |
| `StyleProvider` | `CellFormatContext` | A `CellStyle` to add, or `null` |
| `SuggestionProvider` | `CellSuggestionContext`: `Sheet`, `Address`, `Text`, `CancellationToken` | The candidates |

## 🖌️ CellStyle

| Name | Description |
|---|---|
| `HorizontalAlignment` | `General`, `Left`, `Center` or `Right` |
| `VerticalAlignment` | `Top`, `Center` or `Bottom` |
| `Bold`, `Italic`, `Underline`, `Strikethrough` | Text decorations |
| `FontSize`, `IndentLevel` | Size in pixels, and indent levels |
| `FontColor`, `FillColor` | CSS colors |
| `CssClass` | An additional class |
| `Borders` | `CellBorders` with `Top`, `Right`, `Bottom` and `Left`; a `CellBorder` has a `BorderLineStyle` (`Thin`, `Medium`, `Thick`, `Dashed`, `Dotted`, `DoubleLine`) and a color |

## 🧷 Binding

```csharp
sheet.Bind(order)
    .Cell("B1", static x => x.Customer)
    .Rows("A4:D6", static x => x.Lines, static rows => rows
        .Column("A", static x => x.Name)
        .Column("B", static x => x.Quantity));
```

Values go to the object as soon as they are committed, and `INotifyPropertyChanged` and `INotifyCollectionChanged` update the sheet.  
`SheetBinding<T>` has `Source`, `Refresh()` and `Dispose()`. `SheetRowsBinding<T>.Create` makes new items for a type without a parameterless constructor.  

## 🧮 Computed cells

```csharp
sheet.ComputeRows("D4:D6", ["B", "C"], static x => x.Get<decimal?>("B") * x.Get<decimal?>("C"));
sheet.Compute("D7", ["D4:D6"], static x => x.Values("D4:D6").OfType<decimal>().Sum());
```

The function runs after a watched cell changes, and reads cells by the address it was registered with, even after rows are inserted. The target cells become read-only.  
It receives `CellComputeContext`: `Sheet`, `Address`, `Get<T>(address)` and `Values(address)`. `SheetComputation` has `Refresh()` and `Dispose()`.  

## 💾 Design

```csharp
var functions = new SheetFunctions();
functions.AddComputation("Total", static x => x.Values("D4:D6").OfType<decimal>().Sum());

var json = SheetDesignJson.Write(sheet.ToDesign(functions));
var copy = Sheet.FromDesign(SheetDesignJson.Read(json), functions);
```

`ToDesign` keeps sizes, frozen rows and columns, rules in order, merged cells, labels and computed cells, and `includeValues` adds the input values. Functions are saved by the name in `SheetFunctions`.  
`SheetFunctions` has `AddValidator`, `AddCorrector`, `AddFormatter`, `AddStyleProvider`, `AddSuggestionProvider` and `AddComputation`, and gets them back by name with `Validator`, `Corrector`, `Formatter`, `StyleProvider`, `SuggestionProvider` and `Computation`. `SheetDesign` and the `…Design` types are the shape of the JSON.  

## 🌐 Options

`services.AddMusselSheet(options => …)` sets what applies to every sheet.  

| Name | Description |
|---|---|
| `Cultures` | `SheetCulture` by culture name: `Texts`, `DateFormat`, `FirstDayOfWeek` and `JapaneseDates`. `SheetTexts.English` and `SheetTexts.Japanese` are built in; change them with `with` |
| `Defaults` | `MusselSheetDefaults`: default values of the parameters of the view, under the same names |
| `ReadyKeys`, `EditingKeys` | A key such as `Ctrl+K` to a `SheetCommands` value. `null` leaves the key to the browser |
| `Menu` | `Hidden` commands and additional `Items` (`SheetMenuItem`) of the context menu |
| `Eras` | `SheetJapaneseEra`: `Name`, `ShortName`, `Initial` and `Start`. `SheetJapaneseEra.BuiltIn` is the default |

`SheetCommands`: `Edit`, `ClearContents`, `Undo`, `Redo`, `SelectAll`, `FillDown`, `FillRight`, `Today`, `FillSelection`, `Menu`, `OpenList`, `MoveNext`, `MovePrevious`, `TabNext`, `TabPrevious`, `Cancel` and `NewLine`.  

## 🎨 Theme

Link `_content/MusselSheet/musselsheet.css` in the page head; the sheet needs it. Colors come from CSS variables, and an app changes them by setting the variables, from any selector and in any file order.

These are shared with MusselChart. Set them on any container to theme both libraries.

| Variable | Use |
|---|---|
| `--mussel-primary-text`, `--mussel-secondary-text`, `--mussel-muted-text` | Cell text; header and read-only text; placeholders and disabled items |
| `--mussel-grid-line` | Grid lines and the outer border |
| `--mussel-tooltip-background`, `--mussel-tooltip-border`, `--mussel-tooltip-text` | Lists, calendars, context menus and custom editors |
| `--mussel-critical` | Error marks and messages |
| `--mussel-font-family` | Font family |

These are for the sheet only. Set them on the sheet itself: on `.mussel-sheet`, or on a `class` given to `MusselSheetView`.

| Variable | Use |
|---|---|
| `--mussel-sheet-text-color`, `--mussel-sheet-header-color`, `--mussel-sheet-readonly-color`, `--mussel-sheet-muted-color`, `--mussel-sheet-grid-color`, `--mussel-sheet-error-color`, `--mussel-sheet-popup-bg`, `--mussel-sheet-popup-border`, `--mussel-sheet-popup-color` | The shared colors above, for this sheet only |
| `--mussel-sheet-font` | A `font` shorthand |
| `--mussel-sheet-cell-bg`, `--mussel-sheet-header-bg`, `--mussel-sheet-readonly-bg` | Backgrounds of cells, headers and read-only cells |
| `--mussel-sheet-border-color`, `--mussel-sheet-outer-border` | Cell borders without a color, and the `border` of the sheet |
| `--mussel-sheet-active-color`, `--mussel-sheet-inactive-color` | The active cell and the fill handle, with and without focus |
| `--mussel-sheet-selection-bg`, `--mussel-sheet-selection-border`, `--mussel-sheet-fill-range-color` | The selected range, and the range being filled |
| `--mussel-sheet-check-size`, `--mussel-sheet-check-color`, `--mussel-sheet-check-border` | Checkboxes |
| `--mussel-sheet-freeze-color`, `--mussel-sheet-changed-color` | The line of frozen rows and columns, and the mark of `ShowChanges` |
| `--mussel-sheet-focus-bg`, `--mussel-sheet-popup-shadow` | The highlighted item of a popup, and the shadow of popups |
| `--mussel-sheet-sunday-color`, `--mussel-sheet-saturday-color` | Weekends of the calendar |
| `--mussel-sheet-note-bg`, `--mussel-sheet-note-color`, `--mussel-sheet-note-border` | The note of `InputMessage` |
| `--mussel-sheet-sticky-top` | Where the column headers stop when the page scrolls a sheet without `Height`. Under a fixed page header, set its height here and as `scroll-padding-top` of `html` |

There is no dark theme yet, so the shared variables do not reach a sheet inside a `mussel-dark` container of MusselChart, and the sheet stays light there. To make a sheet dark, set both groups on the sheet itself.

## ⚠️ Limits

| Item | Limit |
|---|---|
| Render mode | Needs an interactive render mode (Server, WebAssembly or Auto). Static rendering only shows the values |
| Content Security Policy | `style-src` needs `'unsafe-inline'`: the layout is written in `style` attributes and a `<style>` element, and the cells lose their size without it. `script-src 'self'` is enough for the script |
| Size | Tested up to 10,000 rows x 50 columns |
| Paste | Up to 16 MB of text |
| Suggestions | The first 50 candidates of `SuggestionProvider` |
| Focus | Focus moving out of the sheet commits the edit, so a button calling `CommitEditAsync` or `CancelEditAsync` must keep the focus (`@onmousedown:preventDefault`) |
| Paste from the context menu | Uses the browser's clipboard permission. When it is refused, a message asks for Ctrl+V |

## 🚧 Not supported yet

Formulas, xlsx import and export, workbooks with several sheets, touch input, screen reader support beyond basic ARIA, and a dark theme.  
