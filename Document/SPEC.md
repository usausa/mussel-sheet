# MusselSheet specification

The behaviour of the sheet. Parameters, types, theme variables and limits are in [API.md](API.md).

## 📚 Terms

An input cell accepts user input. An input-disabled cell has `ReadOnly` or `Kind = Label`, and can still be selected and copied. A covered cell is a cell of a merged range other than its top left cell.

| State | Meaning | Entered by |
|---|---|---|
| Ready | The cell is selected | Loading, commit, cancel |
| Input | Typed text replaces the value. Arrows commit and move | A character, IME composition, Backspace, Ctrl+;, F2 in edit |
| Edit | The value stays and can be changed. Arrows move the caret | F2, double-click, Alt+Down, `BeginEditAsync`, F2 in input |
| Custom | The custom editor is open | F2, double-click, Alt+Down, ▼, `BeginEditAsync` on a `Custom` cell |

With `ReadyEnterMode.Edit`, Enter also starts edit or opens the custom editor.

While the sheet waits for C# (a `Wait` commit, a range paste, a fill, Ctrl+arrows, a row added by Enter, an insert or delete from the menu), characters, arrows, Enter, Tab, Esc, Backspace, Delete, F2, Home, End, PageUp, PageDown, Ctrl+Z and Ctrl+Y are queued and run afterwards. Other keys, the mouse and IME input are ignored.

## 🧾 Sheet

Cells in one column can differ in kind, rules and style. By default rows are 24 pixels high and columns 100 wide. Row and column numbers start at 0. An address is `B2`, `B2:D5`, `B:D` (whole columns) or `2:4` (whole rows), with letters in either case and corners in any order. `sheet.Cells` is the whole sheet.

- `SetValue` stores any object as it is. `ReadOnly` limits the user, not the app.
- `GetValue<T>` converts on reading: `IConvertible` types, `DateTime` and `DateOnly`, `TimeSpan` and `TimeOnly`. It throws when it cannot, also for an empty cell with a non-nullable `T`. `TryGetValue<T>` returns `false` instead.
- An error mark is a message on a cell; the value stays. A failed commit or `SetError` sets it. `SetValue`, `SetValues`, `ClearContents` and a valid commit remove it.
- The first user change of a cell remembers the previous value as the original and starts a change mark. The mark ends when the value equals the original again, even if the app set it, and at `AcceptChanges`. App values start no mark.
- Error and change marks move with their row and column.
- `Changed` reports every change. Inside `BeginUpdate` they arrive as one `Reset` when the outermost scope ends.

| Text | Content |
|---|---|
| `Text` | The value formatted by kind and `Format`, then by `Formatter` |
| `EditText` | `InputText` if there is one, otherwise the edit form of the value |
| `InputText` | The committed text after normalization. Empty text, a date without a year and a corrected value keep none. `SetValue`, `Sheet.Culture` and `Sheet.Options` remove it |

Edit form by kind: `Number` uses format `G`, without group separators. `Date` uses `SheetCulture.EditDateFormat` if set, otherwise the culture's short date pattern with a 4-digit year, or `yyyy/MM/dd`, `MM/dd/yyyy` or `dd/MM/yyyy` by field order when the pattern has other characters than `y M d / . -`. `Time` uses `HH:mm`, or `HH:mm:ss` when seconds are not 0. `Checkbox` uses `true` or `false`, and nothing without a value. `List` and `AutoComplete` use the item's display text.

> Note: A sheet and its view follow `Sheet.Options`, and a view takes `Defaults` once, from the first sheet it shows. If it is not set, the sheet uses the registered options once a view shows it, so `Text` and `EditText` are then what the view shows; until then the sheet uses the built-in languages. To read text with no view, pass the registered options: `new Sheet(rows, columns) { Options = options }`.

### Rows and columns

| Inserting rows (columns alike) | `inheritSettings: true` (default) | `false` |
|---|---|---|
| Source | The row above; at the start, the old first row | None |
| Height | From the source | Default |
| Rule covering the source | Copied to the new rows with the same priority | Not copied |
| Rule spanning the position | Grows | Is split around the new rows |
| Whole-column rule, whole-sheet rule | Applies to the new rows | Applies to the new rows |

Values are never inherited. Deleting shrinks rules and drops empty ones. These methods are not undo steps.

### Merged cells

- Only the app merges. `Merge` keeps the top left value and error mark. The others are removed and do not return after `Unmerge`.
- `Merge` throws for a single cell, a range reaching outside the sheet (whole rows and columns included), and a partial overlap with another merge. Merges inside the range are absorbed. `Unmerge` ends every merge touching the range.
- The settings of the top left cell apply to the whole range.
- Inserting inside a merge grows it, and inserting at or before its first row or column shifts it. Deleting shrinks it, and a merge reduced to one cell ends.

## ⚙️ Settings

`Configure` adds a rule: a range with some settings. A cell uses all rules that contain it, in the order they were added.

- Later rules win item by item, and unset items keep earlier values. Each style item and border side is one item.
- `false` turns an option off (`ReadOnly = false`), `CellBorder.None` removes a border side, and an empty `MoveGroup` or `InputMessage` removes the earlier value.
- Without rules a cell is `Text`, editable, aligned `General` and vertically centered.

## 🧰 Cell kinds

| Kind | Value | Accepted input | Shown as |
|---|---|---|---|
| `Text` | `string` | Any text | The text |
| `Label` | Set by the app | None | The value as text |
| `Number` | `decimal` | Sign, digits, group and decimal separators of the culture. At least one digit, no exponent: `1,234`, `-1.5`, `.5`, `+7` | `Format`, or `G` |
| `Date` | `DateOnly` | See [Dates](#-dates) | `Format`, or `SheetCulture.DateFormat`, or the short date pattern |
| `Time` | `TimeOnly` | `H:mm` or `H:mm:ss` | `Format`, or `HH:mm` |
| `Checkbox` | `bool` | Space, or a click on the box. `true` or `false` in any case from a paste or fill | A box, or `CheckedText` and `UncheckedText` |
| `List` | The item's `Value` | An item's value or display text, in any case and width. Any text without `Items` | The item's display text |
| `Mask` | `string` | Text that fits `Mask` | The text |
| `AutoComplete` | `string` | Any text | The text, or the display text of the item with that value |
| `Custom` | Any | See [Custom cells](#-custom-cells) | `CustomDisplay`, or the value as text, with `Format` if formattable |

- Empty text is `null` in every kind. Text starting with `=` is ordinary text.
- A value of another type shows as text. In a `Checkbox`, anything but `true` or the text `true` in any case is unchecked. `Date` also shows `DateTime` and `DateTimeOffset`, and `Time` also `TimeSpan` and `DateTime`.
- Without `CheckedText` and `UncheckedText`, the text of a `Checkbox` is `☑` or `☐`, and `☐` for no value. `GetText`, `Formatter` and `CellDisplayContext.Text` get this text.
- With only one of `CheckedText` and `UncheckedText`, the other state shows nothing, and its click area is the size of the box.
- `Label` is not drawn like a read-only cell.

| Rule | Applies to | Check |
|---|---|---|
| `Required` | All | Not empty |
| `Minimum`, `Maximum` | `Number`, `Date`, `Time` | Inclusive. A limit of another type is ignored |
| `MaxLength` | Strings | Unicode characters, not UTF-16 units |
| `MaxByteLength` | Strings | ASCII and Unicode's half-width forms count 1, others 2 |
| `CharacterRestriction` | `Text`, `Mask`, `AutoComplete` | Every character must be in the `SheetCharacterSet`. `SheetCharacters.Fullwidth` excludes ASCII and Unicode's half-width forms, `Halfwidth` allows only those, `Alphanumeric` and `Digits` are ASCII, and `None` lifts a restriction of an earlier rule. `JapaneseCharacters.Hiragana` and `Katakana` also allow `ー` and spaces, `Katakana` also `・`, and `HalfwidthKatakana` also the ASCII space |
| `Mask` | `Text`, `Mask` | `9` is a digit, `A` an ASCII letter, `X` either, and `\c` the literal `c`. Other characters are separators the input may omit or replace with an ASCII symbol: for `999-9999`, `1234567` and `123 4567` store `123-4567`. An empty editor shows the mask |
| `Pattern` | All | A regular expression over the whole text, read by the rules of JavaScript on both sides, so `\d` and `\w` are ASCII. A pattern those rules cannot read is ignored, and a match over 200 ms fails |

## 🔁 From text to value

| # | Step | Detail |
|---|---|---|
| 1 | Line breaks | LF with `WrapText`, otherwise removed |
| 2 | Width | `Number`, `Date`, `Time`, `Checkbox` and masked cells: full-width ASCII becomes half-width, the ideographic space a space, the digits of every script `0` to `9`, `−` becomes `-`, bidirectional marks are dropped, the language's `Readings` apply, and the text is trimmed. `Date`: `DateReadings` also apply. `List`: trimmed only |
| 3 | Mask | Text that fits takes the mask's form. Other text fails at step 6 |
| 4 | Conversion | To the kind's value type. Empty text becomes `null`. A failure skips to step 7 |
| 5 | `Corrector` | Runs also for `null`, and returns the value to use. A corrected string is the text for step 6, and the edit text of a corrected value is the `Text` of steps 8 and 10 |
| 6 | Rules | The first failure wins: `Required`, `Minimum` and `Maximum`, `MaxLength`, `MaxByteLength`, `CharacterRestriction`, `Mask`, `Pattern` |
| 7 | `ErrorMessage` | Replaces the message of steps 4 and 6 |
| 8 | `Validator` | Runs when there is no error. Its message stays as it is |
| 9 | Binding | A bound value must fit the property |
| 10 | `CellCommitting` | Can change `Value` and `Error`, or set `Cancel` |
| 11 | `ValidationMode` | Decides what an error does |
| 12 | Store | Skipped when the value and error mark are unchanged. Otherwise stores them, then bindings and computed cells run |
| 13 | `CellCommitted` | Reports `OldValue`, `Value`, `Error` and `Reason` |

Step 2 is not a setting: it always runs on those cells, only to read the value. The sheet does not convert the text of `Text`, `AutoComplete` and `Custom` cells. To convert width or kana, an app uses a `Corrector`, for example with `Usa.Smart.Text.Japanese`:

```csharp
c.Corrector = static x => x.Value is string text
    ? KanaConverter.Convert(text, KanaOption.HiraganaToKatakana | KanaOption.HankanaToKatakana)
    : x.Value;
```

The browser runs steps 1 to 4, 6 and 7 first. For a cell with a `Corrector`, it checks only the type, so a rule that fails after the `Corrector` fails in C#: with `Optimistic` the sheet has already moved, and the value is stored with an error mark.

| Action | Steps | `Reason` |
|---|---|---|
| Typing, a popup pick, Ctrl+Enter | All | `Input` |
| Custom editor | From step 5, keeps no `InputText` | `Input` |
| Paste; Ctrl+D, Ctrl+R, fill handle; checkbox toggle | All | `Paste`; `Fill`; `Toggle` |
| Delete; cut | From step 10, with `null` | `ClearContents`; `Cut` |
| Undo; redo | Only 12 and 13 | `Undo`; `Redo` |
| `SetValue` and other app methods | None, and no events | |

## 🚦 Commit and validation

Focus moving to a focusable element outside the sheet commits without a move, and a `Stay` error there stores the value with an error mark. Moving to another window or clicking something that cannot take focus keeps the state.

| `CommitMode` | Behaviour |
|---|---|
| `Optimistic` | The browser checks the text and moves at once. C# runs the steps afterwards. Focus leaving the sheet always works this way |
| `Wait` | The browser checks the text, C# runs the steps, and the move follows the result. A rejected commit drops the queued keys |

`CellSettings.ValidationMode` overrides the mode of the view.

| `ValidationMode` | Error in the browser check | Error only in C#, `Optimistic` | Error only in C#, `Wait` |
|---|---|---|---|
| `Stay` | Stays with the message, until fixed or Esc | Stored with an error mark | Stays with the message |
| `Revert` | Drops the text and moves | The old value stays, no mark | The old value stays, and moves |
| `AcceptAndMark` | Stored with an error mark, and moves | Stored with an error mark | Stored with an error mark, and moves |

- Text that cannot be converted is stored as a string.
- `Cancel` in `CellCommitting` stores nothing. The message is `Error`, or else the `Canceled` text. In `Optimistic` it becomes the error mark. In `Wait` the cell stays with the message, and a `Revert` cell moves.
- A range paste, Ctrl+Enter, Ctrl+D, Ctrl+R and the fill handle run the steps per cell. `Stay` and `AcceptAndMark` store with an error mark, `Revert` leaves the cell, and a message gives the counts.
- If the active cell becomes input-disabled during input or edit, the typed text is dropped.
- `SetActiveCellAsync` commits an edit first. `BeginEditAsync` works in ready. `CommitEditAsync` commits without a move.

## ⌨️ Keys

Ctrl also means ⌘, and `—` leaves the key to the browser. On an input-disabled cell, keys that start input or edit or clear the value do nothing. Characters do nothing on `Checkbox` and `Custom` cells. In ready, Shift with Home, End, PageUp, PageDown, Ctrl+Home, Ctrl+End or Ctrl+arrows extends the selection instead of moving.

| Key | Ready | Input | Edit |
|---|---|---|---|
| Character, IME, Space | Starts input. Space toggles a `Checkbox` | Types | Types |
| F2 | Starts edit or opens the custom editor | Switches to edit | Switches to input |
| Enter, Tab, with or without Shift | Moves | Commits and moves | Commits and moves |
| Esc | Nothing | Cancels | Cancels |
| Arrows | Moves | Commits and moves | — |
| Shift+arrows | Extends the selection | Commits and extends | — |
| Ctrl+arrows | Jumps to the data edge | Commits and jumps | — |
| Home, End | First, last column of the row | — | — |
| Ctrl+Home, Ctrl+End | The home cell, the last used cell | — | — |
| PageUp, PageDown | Moves a screen | Commits and moves | Nothing |
| Delete | Clears the cell or selection, without rules | — | — |
| Backspace | Starts input with empty text. The value stays until the commit | — | — |
| Alt+Down, Alt+Up | Alt+Down opens the popup or custom editor | Opens, closes the popup | Opens, closes the popup |
| Alt+Enter | Nothing | A line break with `WrapText` | A line break with `WrapText` |
| Ctrl+Enter | With a range, fills it with the active cell's edit text | Commits without a move, or fills the range | The same |
| Ctrl+D, Ctrl+R | Fill down, fill right | — | — |
| Ctrl+; | Starts input with today in edit form | Inserts today at the caret | The same |
| Ctrl+A | Selects all. The active cell stays | — | — |
| Ctrl+Z; Ctrl+Y, Ctrl+Shift+Z | Undo; redo | — | — |
| Ctrl+C, Ctrl+X | Copy, cut | — | — |
| Ctrl+V | Paste | Inserts the first value at the caret | The same |
| Shift+F10, menu key | Opens the context menu | — | — |

- Ctrl+Enter with a range fills it with the typed text, and the selection and active cell stay. A browser-check error in a `Stay` active cell stops it.
- Ctrl+D with one row selected copies the row above, and with several copies the first row to the others. Ctrl+R does the same with columns. The source's edit text runs the steps of each target. The selection stays.
- The editor grows right with long text, and down with `WrapText`.

`ReadyKeys` applies in ready, `EditingKeys` in input and edit. A listed key acts as the default key of its `SheetCommands` value, and `null` leaves it to the browser. Unlisted keys keep their action, so a command's default key still works unless it is also set to `null`. Write keys as `Ctrl+Alt+Shift+Key`, in any order and case, where `Key` is a character, `Space` or a `KeyboardEvent.key` name. Default keys: `Edit` F2, `ClearContents` Delete, `Undo` Ctrl+Z, `Redo` Ctrl+Y, `SelectAll` Ctrl+A, `FillDown` Ctrl+D, `FillRight` Ctrl+R, `Today` Ctrl+;, `FillSelection` Ctrl+Enter, `Menu` Shift+F10, `OpenList` Alt+Down, `MoveNext` Enter, `MovePrevious` Shift+Enter, `TabNext` Tab, `TabPrevious` Shift+Tab, `Cancel` Esc, `NewLine` Alt+Enter.

## 🗾 IME

- Composition can start in ready, and its first character goes to the cell.
- During composition the sheet ignores every key, also in popups, and suggestions wait until it ends.
- The Enter that confirms a composition does not commit. The next Enter does.
- The IME window appears at the active cell.
- `ImeMode.Off` sets `inputmode="url"` on input cells. The effect depends on the browser and the IME.

## 🧭 Movement

The first input cell is active after loading, or A1 if there is none. The sheet does not take focus on loading; `FocusAsync` gives it. A move scrolls the cell clear of the headers and frozen parts.

| Key | Goes to |
|---|---|
| Enter with `Down` | The next cell below. Stops at the end of the column |
| Enter with `Right` | The next cell to the right, then by `RowEndMode` |
| Enter with `None` | Stays |
| Tab | The next cell to the right, then by `RowEndMode` |
| Shift+Enter, Shift+Tab | The same path backward |
| Arrows | The next cell. Stops at the sheet edge |
| PageUp, PageDown | The same column, by the visible rows minus one |
| Ctrl+arrows | From a filled cell with a filled next cell: the end of that run. Otherwise: the next filled cell, or the sheet edge. Empty means `null` or `""` |
| Ctrl+Home | The first cell outside the frozen parts. With `SkipReadOnlyMode.All`, the first input cell |
| Ctrl+End | The cell at the last row with data and the last column with data |

- `RowEndMode` at the last input cell of a row: `NextRow` goes to the first input cell of the next row that has one, and stops at the end of the sheet. `Stop` stays. With `LeaveSheet`, Tab commits and the browser moves focus out, and Enter stays.
- `SkipReadOnlyMode`: `EnterAndTab` makes Enter and Tab skip input-disabled cells, `All` also arrows, `None` neither. Home, End, PageUp, PageDown, Ctrl+arrows and clicks can stop on any cell.
- `Right`, `EnterAndTab` and `NextRow` together walk the input cells row by row and pass over rows without any.
- `TabReturnEnabled`: after Tab moved right in a row, Enter with `Down` goes below the column where the Tabs started. Shift+Tab, a Tab to another row and any other move end this.
- A merge moves as one cell. A move into it stops at the top left cell, and a move out of it starts at its far edge. Covered cells are never stops.

**Selected range.** It comes first for Enter and Tab. They stay inside and cycle from the last cell to the first, and the selection stays. Tab goes row by row. Enter goes column by column with `Down`, row by row with `Right`, and not at all with `None`. Shift reverses. `SkipReadOnlyMode` applies. No row is added, and Tab does not leave the sheet.

**Move group.** It comes next. Cells with the same `MoveGroup` work like a selected range, without Tab return. From a cell without a group, Enter and Tab pass over cells that have one. Arrows and the mouse ignore groups.

**Last row.** With `LastRowEnterMode.AddRow`, Enter in an input cell adds a row when it finds no next cell by the normal rules and `RowsInserting` does not cancel. Shift+Enter and `None` add nothing. The row goes directly below the active row, with its settings and height, and Enter's move then runs again. The commit and the row are two undo steps, and undoing the row returns to the cell.

> Note: With `Right` and `RowEndMode.Stop` or `LeaveSheet`, every row end has no next cell, so Enter adds a row there.

## 🖱️ Mouse

| Action | Result |
|---|---|
| Click a cell | It becomes active. In input or edit the commit comes first, and a `Stay` error keeps the state |
| Click a checkbox's box or text | It becomes active and toggles. The rest of the cell only selects |
| Double-click a cell | Starts edit or opens the custom editor. A `Checkbox` does not start edit |
| Click ▼ | Opens or closes the list or calendar, or opens the custom editor. ▼ appears beside an active `List` or `Date` input cell, and a `Custom` input cell with a template |
| Shift+click, drag | Extends the selection, and a drag scrolls at the edges. Shift+click in input or edit commits and moves instead |
| Click a header | Selects the row or column. The active cell goes to its first visible cell. Shift or a drag extends by whole rows or columns |
| Click the top left corner | Selects all. The active cell stays |

## 🔲 Selection

- Shift+keys, Shift+click and drags move one corner, and the active cell stays. Arrows and clicks end the range.
- A range grows until it holds every merge it touches. Shift+arrows step over a merge.
- `ActiveCellChanged` and `SelectionChanged` come together 50 ms after the last change. With no range, `Selection` is the active cell or its merge.

## 📋 Clipboard

Copy works in ready and uses the edit text. Read-only cells can be copied, and covered cells are empty.

| Copied | `text/plain` | `text/html` |
|---|---|---|
| One cell, by key | The edit text | A one-cell table |
| One cell, from the menu | The edit text | None |
| A range | Tab-separated, CRLF after every row. A cell with a tab, a line break or a leading `"` is quoted, with `"` doubled | A table |

In the table, `Text`, `Mask`, `AutoComplete` and `List` cells carry `mso-number-format:"\@"`, and line breaks become `<br>`.

> Note: `mso-number-format` makes Excel keep leading zeros and a leading `=` as text. Copying undrawn cells uses the asynchronous clipboard, and if the browser refuses, nothing is copied.

> Note: The edit text of a typed cell is the committed text: a cell committed as `1,234` copies as `1,234`.

> Note: Copy does not escape a leading `=`, so without the table Excel may read such text as a formula.

Cut copies and clears at once, without rules. Input-disabled cells are copied but not cleared.

Paste reads only `text/plain`. One trailing line break is dropped. Tabs separate cells, and CRLF, LF or CR separate rows. A cell that starts with `"` and has a closing `"` before a tab, line break or the end is quoted: it can hold tabs and line breaks, and `""` is a quote.

| Clipboard | Selection | Result |
|---|---|---|
| One value | One cell | Like typing and committing, without a move. On an error, `Stay` enters input with the text and message (`Checkbox` and `Custom`: the message only), `Revert` shows the message, and `AcceptAndMark` stores with an error mark |
| One value | A range | Every cell gets the value |
| A table | Any | Starts at the top left of the selection, whatever its size |

Range paste, in the last two rows:

- `Pasting` comes first, with `Range` and `Values`, and `Cancel` stops it.
- Anything outside the sheet is cut, and a message gives the counts. No rows or columns are added.
- Cells beyond a short row stay unchanged. Input-disabled and covered cells are skipped. Settings never change.
- The pasted range ends up selected, with the active cell at its top left. It is one undo step.

In input and edit, paste inserts the first cell of the table at the caret. In custom it is left to the browser. The menu's Paste reads the asynchronous clipboard, then works the same; if that fails, a message asks for Ctrl+V.

## 🪣 Fill handle

The handle sits at the bottom right of the selection, in ready.

- A drag fills in one direction: the one with more overshoot, vertical on a tie. Dropping inside the source does nothing, and Esc cancels.
- Holding Ctrl or Alt on release swaps copy and series.
- A double-click fills down while the column to the left (or else to the right, if the left is empty) has data, and stops before a row where the target has a value.
- `Filling` comes first, with `Source` and `Target`, and `Cancel` stops it.
- Each target cell runs its own steps, and input-disabled and covered cells are skipped. Source and target end up selected, and the active cell stays. A fill is one undo step.

Each column, or row for a horizontal fill, is its own series:

| Source | Fill | With Ctrl or Alt |
|---|---|---|
| One number | Copy | Steps by 1 |
| Several numbers | By equal spacing, keeping the digits (`0.1, 0.2` to `0.3`). Otherwise a least-squares line | Copy |
| One date | Steps by 1 day | Copy |
| Several dates | With the same day and an equal month step, by months; a missing day becomes the last of the month (`1/31, 3/31` to `5/31`, backward `11/30`). Otherwise by equal spacing or a least-squares line in days | Copy |
| One text with a number | The last number steps by 1 (`A-001` to `A-002`) | Copy |
| Several texts with numbers | The same text: by spacing (`A-001, A-003` to `A-005`), or a rounded least-squares line. Different texts: repeats, adding 1 each round (`A1, B1` to `A2, B2, A3`) | Copy |
| Anything else | Repeats the source | Copy |

The number in a text is its last run of half-width digits, up to 18. Zero padding stays, and a negative result uses the absolute value. Filling up or left runs the series backward. A copy uses the source's edit text.

## 📑 Context menu

It opens by right-click, Shift+F10 or the menu key. A right-click outside the selection first selects that cell, row or column. In input and edit, the editor keeps the browser's menu, and a right-click elsewhere commits first. Popups and the custom editor keep the browser's menu.

| Opened on | Items |
|---|---|
| A cell | Cut, copy, paste, row commands, column commands |
| Whole rows selected, or a row header | Cut, copy, paste, row commands |
| Whole columns selected, or a column header | Cut, copy, paste, column commands |
| The top left corner | Selects all, then as for a cell |

- Row commands insert above, insert below and delete. Column commands insert left, insert right and delete. Each needs its `Allow…` parameter, and its label shows the count.
- Cut and paste are disabled when the range has no input cell. Delete is disabled when every row or column is selected.
- `Menu.Hidden` hides `cut`, `copy`, `paste`, `insertRowsAbove`, `insertRowsBelow`, `deleteRows`, `insertColumnsLeft`, `insertColumnsRight` or `deleteColumns`. `Menu.Items` adds items at the end, and a click raises `MenuItemClicked`.
- Up and Down go to the next enabled item and wrap. Home and End go to the first and last. Enter and Space run the item. Esc and Tab close. Other keys close it and then act normally. Opening by key highlights the first enabled item.
- A click outside, focus leaving, a window resize or IME composition closes it.

`RowsInserting`, `RowsDeleting`, `ColumnsInserting` and `ColumnsDeleting` come first, and `Cancel` stops them. New rows and columns inherit from the first selected one when inserted above or left, and from the last when below or right. The inserted part, as wide as the old selection, ends up selected, with the active cell in its first row or column. After a delete, the same position stays selected.

## ⏪ Undo

Each view has its own history, up to `UndoLimit`, and a new `Sheet` clears it. Each user operation is one step: value changes and rows or columns the user inserted or deleted. Anything the app does, binding updates and computed values are not recorded.

- A value step restores the value, `InputText` and error mark, and selects the cell, or for a range operation the whole operated range. It raises `CellCommitted` with `Undo` or `Redo`, runs no rules and raises no `CellCommitting`.
- A structure step brings back the same rows and columns with values, error marks, sizes, settings and merges, and restores the selection. It raises no `RowsInserting` or similar events.
- Steps follow their rows and columns through the app's inserts and deletes. A cell that is gone is skipped.
- Rules the app appended after a structure step stay. After other rule changes, returning rows inherit from an adjacent row.
- A structure step does nothing if the app removed its rows or the row above them, or inserted rows between them.

## 🔽 Popups

Popups appear below the cell, or above when there is no room. Focus stays in the editor. Alt+Down also closes an open popup. A click on an item or day commits without a move.

| Key | List, suggestions | Calendar opened by Alt+Down or ▼ |
|---|---|---|
| Up, Down | Previous, next item | Same day of the previous, next week |
| Left, Right | The caret in edit. In input they commit and move | Previous, next day |
| PageUp, PageDown | 8 items up, down | Previous, next month |
| Enter, Tab | Commits the highlighted item and moves. Without one, the typed text | Commits the highlighted day and moves |
| Esc | Closes. A second Esc cancels | Closes. A second Esc cancels |

- **List.** Alt+Down and ▼ show all items, with the current one highlighted. Typing filters on value or display text in any case, prefix matches first, and highlights the first. With no match the popup closes.
- **Auto complete.** For non-empty text, suggestions appear 250 ms after typing stops or a composition ends. Alt+Down shows them at once, also for empty text. A newer request cancels the older one. Without `SuggestionProvider`, `Items` is filtered like a list. Nothing is highlighted at first, so Enter commits the typed text.
- **Calendar.** Days outside `Minimum` and `Maximum` cannot be picked. The highlight starts on the cell's date or today, or else the nearest allowed day. Buttons change the month or pick today.
- **Calendar in edit.** F2 and double-click show a calendar that only follows the typed date: keys edit the text and Esc cancels. Alt+Down makes it the keyboard calendar. Clicks work in both.

## 🧩 Custom cells

The `CustomEditor` template opens with F2, double-click, Alt+Down or ▼, never by typing. `Editor` of the cell arrives as `context.Name`. Without a template the cell cannot be edited, but Delete and paste still work. Focus goes to the element with `autofocus`, the first focusable element, or the editor.

- Enter (not with Alt or Ctrl, not on a button or link), Tab and Shift+Tab commit `context.Value` and move. Esc cancels. Keys the template handled with `preventDefault` stay with it.
- `context.CommitAsync` commits without a move, and returns whether it stored without an error. `context.CancelAsync` cancels.
- A click on another cell commits and moves. Focus leaving the sheet commits.
- On an error, `Stay` keeps it open with the message in `context.Error`, `Revert` closes it with the old value, and `AcceptAndMark` closes it with an error mark.

## 🧷 Binding

`Bind` links cells to an object and leaves the cell settings to the app.

- At binding, the property value goes to the cell.
- Every value change goes to the property at once, also inside `BeginUpdate` and from `SetValue`. If the property then reads differently, the cell shows that.
- `INotifyPropertyChanged` and `INotifyCollectionChanged` update the sheet. `Refresh()` shows plain objects again. `Dispose()` ends the link, and values stay.
- Conversion follows `GetValue<T>`, and an enum is read from text in any case. An empty cell writes `null`, or the type's default, which the cell then shows. A fraction does not fit a whole number.
- In a commit, a value that does not fit is an error with a message for the property type, and a property without a public non-`init` setter gives `NotEditable`. A value from the app that does not fit is not written.
- A setter's exception message becomes the error mark.
- Cells follow their row and column. A cell whose row or column is deleted rests until undo.

`Rows` maps the rows of a range to list elements from the top. The range must be inside the sheet, and each `Column` inside the range.

| Event | Result |
|---|---|
| Fewer, more elements than rows | The other rows stay empty; rows are inserted like the detail rows |
| A value entered in an empty row | A new element at that position, from `Create` or the parameterless constructor. Blank columns show the element's values |
| A value cleared in a row | It stays an element |
| The app adds an element | The next empty row after the previous element, or an inserted row |
| The app removes an element | Its row is deleted, and a new empty row at the end keeps the original count |
| The app replaces an element, resets or replaces the list | The row, or all rows, show it |
| A row inserted below a detail row, or above the first | It joins the detail as an empty row |
| The user deletes a row | Its element leaves the list. Undo returns it to the same position |

A read-only list gets no new elements. Without `Create` or a parameterless constructor, binding throws.

> Note: If the user deletes every row of the detail, it has no rows left. An app can keep one with `RowsDeleting`.

## 🧮 Computed cells

`Compute` targets one cell, and `ComputeRows` one column of rows.

| Item | Rule |
|---|---|
| Watched | The listed cells and ranges. In `ComputeRows`, a column name means the same row, and an address the same cell for every row |
| Reading | `Get<T>` and `Values` take the name given at registration, in any case, even after rows move. Other names throw. `Get<T>` reads the first cell, and gives the default when it has no value or does not convert |
| Target | Read-only by a rule added at registration. A later `ReadOnly = false` opens it |
| Runs | At registration, and after a watched value changes. Inside `BeginUpdate`, once at the end. `ComputeRows` runs only changed rows, or all rows when an address changes |
| Chains, loops | A computed value can be watched by another computation. Computations watching each other stop after one pass |
| Result | Stored like `SetValue` when it differs: no undo step, no change mark. Undo recomputes from the restored values |
| Growth | Ranges of two or more rows and `ComputeRows` targets take rows inserted inside them, directly below the last or directly above the first. A single cell follows that cell. Deleted rows return with undo |
| `Refresh()`, `Dispose()` | Runs it again. Stops it, and the values and read-only rule stay |

## 💾 Design

- Saved: row and column counts, default sizes that differ from 24 and 100, culture name, frozen counts, heights, widths and headers set on rows and columns, rules in order, merges, `Label` values, and computations. With `includeValues`, all values.
- Not saved: error marks, change marks, `InputText`, undo history, parameters of the view, and functions not in `SheetFunctions`, with the computations using them.
- Functions are saved by name, and `FromDesign` takes them from the same `SheetFunctions`. Names are unique across all function kinds, and adding a name again replaces it.
- Character sets are saved by `Name`. `FromDesign` takes them from `SheetFunctions` (`AddCharacterSet`) first, then from the built-in ones.
- A computation saves the name it reads, and the current address when different. A loaded sheet computes at once, and the read-only rule comes from the saved rules.
- Loading throws `JsonException` for a property it does not know, `NotSupportedException` for an unknown `version`, `InvalidOperationException` for a function name without `SheetFunctions`, and `KeyNotFoundException` for a name not in it.
- JSON: camelCase names, no `null` items, LF line breaks. Ranges use addresses, and `*` is the whole sheet. Enums use camelCase names. `characterRestriction` is a name. Limits are text (`0`, `2026-01-01`, `09:00:00`). Values have a type: `text`, `number`, `date`, `time` or `check`. The invariant culture is an empty name.

> Note: Saving finds a name from the delegate, so attach the delegate returned by `SheetFunctions`. The JSON is not escaped for HTML.

## 🌐 Languages

The code of the sheet knows no language. A language is data of one shape, registered in `Cultures`, and it reaches the browser only as data: a language has no script of its own. English and Japanese are built in and need no registration. Japanese is the model for another language: it uses only the public API, so an app adds a language the same way, in any assembly, and can also change or remove a built-in one.

| Follows | What |
|---|---|
| `Sheet.Culture` | Shown and edit text, number and time separators, date field order, calendar names, and all of `SheetCulture` except `Texts` |
| `CurrentUICulture` | `SheetTexts` messages and menu labels, `SheetMenuItem.Texts` |

`Cultures` is searched item by item from the culture to its parents (`ja-JP`, `ja`, then the empty name). An item not set or empty does not count, and an item found nowhere comes from .NET. Built in: the empty name has `SheetTexts.English`, and `ja` has `JapaneseLanguage.CreateCulture()` from `MusselSheet.Languages.Japanese`. Configure the options before the first sheet is shown.

### What every language shares

Only what Unicode and `CultureInfo` decide.

| Item | Rule |
|---|---|
| Width | Full-width ASCII (U+FF01 to U+FF5E) and the ideographic space read as half-width, and `”’￥` are not among them. Half-width means ASCII and Unicode's half-width forms |
| Digits | The decimal digits of every script (`۱۲۳`, `๑๒๓`) read as `0` to `9` |
| Minus | `−` reads as `-`, because many cultures show it |
| Bidirectional marks | U+200E, U+200F and U+061C are not read, because many cultures show them around signs and separators |
| Numbers | The culture's decimal and group separators. When the group separator is a space, the plain, no-break and narrow no-break spaces are all accepted. When both separators are the same, there is no grouping |
| Times | `:` and the culture's separator |
| Dates | The field order of the culture, and a space after `.` and a final `.` (`5. 11. 2025.`) |
| Calendar | A value is always a Gregorian date, and a culture whose default calendar is another one is used with the Gregorian calendar |

`Mask`, `MaxByteLength` and `CharacterRestriction` work in every culture.

### What a language brings

| `SheetCulture` | Use | Japanese |
|---|---|---|
| `Texts` | `SheetTexts`. `CharacterSets` holds the message of each character set by name | `JapaneseTexts.Default` |
| `DateFormat`, `EditDateFormat` | The shown and edit formats of a date. The edit format must be one the language reads | Not set |
| `FirstDayOfWeek` | The first day of the calendar | Not set |
| `Eras` | `SheetEra`: `Name`, `ShortName`, `Initial`, `Start`, and `FirstYear`, the number of the start year (1 by default) | Meiji to Reiwa |
| `EraFirstYear` | `SheetEraFirstYear`: the text for an era's first year, and the unit it stands before | `元`, `年` |
| `DateForms` | The forms of dates it reads. See below | See below |
| `Readings` | A `SheetCharacterMap` applied in step 2 | `ー ‐ ―` to `-` |
| `DateReadings` | The same, for `Date` cells only | `・` to `/` |

Calendars with the same months as the Gregorian one are era tables: Japanese eras, Minguo (`new SheetEra("民國", "民", "", new DateOnly(1912, 1, 1))`) and the Buddhist year (`new SheetEra("พ.ศ.", "พ.ศ.", "", new DateOnly(1, 1, 1)) { FirstYear = 544 }`). Calendars with other months, such as the Persian, Hijri and Hebrew ones, are not supported, and such a culture shows Gregorian dates.

Character sets are cell settings, so they work in any culture. `new SheetCharacterSet(name, characters)` takes characters and ranges in one string (`"0-9A-Za-z"`), a reversed range is an error, and `Message` is its message when `SheetTexts.CharacterSets` has none. Only the sets a sheet uses reach the browser. `new SheetCharacterMap(from, to)` pairs characters by position (`"ёЁ"` to `"еЕ"`), and a single character in `to` takes every character of `from`.

```csharp
public static class KoreanLanguage
{
    public static SheetCharacterSet Hangul { get; } = new("hangul", "가-힣 ") { Message = "한글로 입력하세요." };

    public static MusselSheetOptions AddKorean(this MusselSheetOptions options)
    {
        options.Cultures["ko"] = new SheetCulture
        {
            Texts = new SheetTexts { Required = "값을 입력하세요." },
            DateForms = { "yyyy년 M월 d일", "M월 d일" }
        };
        return options;
    }
}
```

## 📅 Dates

A language's `DateForms` are read first. Then, in every language, separators are `/`, `-` and `.`. The date must exist. The field order comes from the culture's short date pattern.

| Form | Year-month-day | Month-day-year | Day-month-year |
|---|---|---|---|
| 4-digit year first | `2025/11/5` | `2025/11/5` | `2025/11/5` |
| Full | `25/11/5` | `11/5/2025`, `11/5/25` | `5/11/2025`, `5/11/25` |
| No year | `11/5` | `11/5` | `5/11` |
| Digits | `20251105`, `251105`, `1105` | `11052025`, `110525`, `1105` | `05112025`, `051125`, `0511` |

A 2-digit year from `00` to `29` is 20xx, and from `30` to `99` is 19xx. A missing year is the browser's current year, also on Server. A date without a year keeps no `InputText`, so editing it again shows the full date. A day alone (`5`) is rejected.

A form in `DateForms` uses the symbols of a date format. A form without a month and a day, with a field twice, or with an era the culture does not have is left out.

| Symbol | Reads |
|---|---|
| `ggg`, `gg`, `g` | The era's name, short name, and initial in either case |
| `e`, `ee`, `eee`, `eeee` | The era year, of up to 2, 2, 3 or 4 digits, or the first-year text. Without a `g`, the last era |
| `yyyy`, `yy` | A year of 4 or 2 digits |
| `M`, `d` | A month and a day of up to 2 digits |
| `/` | Any of `/`, `.` and `-` |
| A space | Any number of spaces, or none |
| Quotes | Literal text |

In a `ja` sheet, these are also read:

| Form | In `DateForms` | Examples |
|---|---|---|
| `・` as a separator | From `DateReadings` | `2025・11・5`, `11・5` |
| Kanji | `yyyy年M月d日`, `yy年M月d日`, `M月d日` | `2025年11月5日`, `25年11月5日`, `11月5日` |
| Era name | `ggge年M月d日`, `ggge/M/d` | `令和7年11月5日`, `令和元年5月1日`, `令和7/11/5`, `令和7.11.5` |
| Era short name | `gge年M月d日` | `令7年11月5日` |
| Era initial, in any case or width | `ge/M/d` | `R7.11.5`, `r7/11/5`, `H31.4.30` |

An era year past the era's end counts on (`H32.1.1` is 2020-01-01). A date before the era's first day (`令和1年4月30日`), `R071105` and `7.11.5` are rejected.

A `Format` with an unquoted `g` or `e` is an era format. It uses the eras of the sheet's culture, and a culture without eras shows the short date pattern. For 2025-11-05 in Japanese:

| Symbol | Result |
|---|---|
| `ggg`, `gg`, `g` | `令和`, `令`, `R` |
| `e`, `ee` | `7`, `07`. In an era's first year, `e` before `EraFirstYear.Unit` gives `EraFirstYear.Text` (`令和元年`) |
| `yyyy`, `yy`, `M` to `MMMM`, `d` to `dddd` | As in .NET |
| Quotes, `\` | Literal text |

`ggge年M月d日` gives `令和7年11月5日`, and `ge.M.d` gives `R7.11.5`. Dates before the first era use the short date pattern. To add an era, add a row: `options.Cultures["ja"].Eras.Add(new SheetEra("Name", "N", "N", start))`.

## 🖥️ Display

- `Width` and `Height`: a fixed size scrolls inside with headers in view. With both `auto` the sheet does not scroll, and headers and frozen parts stick with the page scroll.
- A fixed `Height` draws only visible rows and `OverscanCount` more, and a fixed `Width` only visible columns and `OverscanColumnCount` more. Row heights may differ. Keys, the selection and the editor also work on undrawn cells.
- `FrozenRows` and `FrozenColumns` of the view override those of the sheet. Counts above the sheet size freeze everything. Inserts and deletes do not change the counts. Frozen parts are always drawn, with a line at the boundary.
- Text is clipped at the cell edge. With `WrapText` it wraps and keeps line breaks. Row height never adjusts.
- `General` aligns `Number`, `Date` and `Time` right, `Checkbox` center, and others left.
- `IndentLevel` adds one character width per level from the aligned side. Centered text ignores it.
- `ReadOnly` cells have their own background and text color, and `FillColor` and `FontColor` win over them.
- A color with characters other than letters, digits, space and `# ( ) , . % -` is ignored.
- A merge draws as one cell in its top left cell's style, centered vertically unless top, bottom or `WrapText`. The active frame and editor cover it, and a visible merge is always drawn.
- Border widths in pixels: `Thin`, `Dashed` and `Dotted` 1, `Medium` 2, `Thick` and `DoubleLine` 3. `ShowGridLines` does not affect borders. Adjacent cells share one line, and the later rule wins. A merge's right and bottom lines come from its top right and bottom left cells.
- `Formatter` receives the formatted `Text` and returns the shown text, or `null` to keep it. `StyleProvider` returns a `CellStyle` added over the cell's style, except borders and vertical alignment. Neither limits input or affects the editor.
- The editor shows the cell's bold, italic, underline, strikethrough, font size and font color, but not alignment, indent or fill.
- Without focus the frame and selection turn grey. The active cell is not shaded.

| Item | Display |
|---|---|
| Error mark | A red inner line, with the message as the tooltip |
| Error in the editor | A red frame, with the message below the cell |
| Change mark | With `ShowChanges`, a small triangle at the top left corner |
| `InputMessage` | Below the active cell while the sheet has focus, or above when a scrolling sheet has no room. Hidden during an error message, a popup, the menu and the custom editor |
| Paste and fill messages | Below the active cell until the next key or move |

## ♿ Accessibility

| Element | ARIA |
|---|---|
| Sheet | `grid`. `aria-rowcount` and `aria-colcount` count headers and undrawn cells |
| Row | `row`, with `aria-rowindex` from 1, counting the header row |
| Headers | `columnheader`, `rowheader`, with `aria-colindex` |
| Cell | `gridcell`, `aria-colindex`. `aria-readonly` when input-disabled, `aria-invalid` with an error mark, `aria-rowspan` and `aria-colspan` on a merge |
| Hidden | The corner, spacers and covered cells have `aria-hidden` |
| Input element | Outside the grid. `aria-label` is `SheetTexts.CellInput`, and `aria-describedby` points to the `InputMessage` |
| Checkbox | The box holds a visually hidden `☑` or `☐` |
| Menu, list | `menu`, `menuitem`, `separator`; `listbox`, `option` |
