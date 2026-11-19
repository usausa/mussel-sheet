import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import {
    shiftCell,
    compileReading,
    normalizeText,
    prepareText,
    checkInput,
    formatMask,
    maskDisplay,
    isAllowed,
    filterItems,
    pageTarget,
    parseDate,
    matchDate,
    parseNumber,
    formatDate,
    addDays,
    addMonths,
    monthGrid,
    formatYearMonth,
    findNext,
    firstInputCell,
    countBytes,
    clipboardCell,
    normalizeLines,
    parseClipboard,
    toTsv,
    toHtml,
    rangeOf,
    nextInRange,
    nextInGroup,
    indexAt,
    menuKind,
    menuEntries,
    comboOf,
    nextMenuIndex,
    paneOf,
    selectionPieces,
    fillTarget,
    frozenIndex,
    SheetMap,
    lineStartOf,
    deleteSize,
    currentItem,
    shiftSelection,
    lineCaret,
} from '../../MusselSheet/wwwroot/MusselSheet.js';

// What C# produces for each language: AddedLanguageTests checks that this file is up to date
const LANGUAGES = JSON.parse(readFileSync(new URL('./languages.json', import.meta.url), 'utf8'));

const SETS = LANGUAGES.sets;
const JA = compileReading(LANGUAGES['ja-JP']);
const TW = compileReading(LANGUAGES['zh-TW']);
const TH = compileReading(LANGUAGES['th-TH']);
const KO = compileReading(LANGUAGES['ko-KR']);
const AR = compileReading(LANGUAGES['ar-SA']);
const US = compileReading({ order: 'mdy', editDate: 'M/d/yyyy' });
const DE = compileReading({ decimal: ',', group: '.', order: 'dmy', editDate: 'dd.MM.yyyy' });

const messages = {
    required: '入力してください',
    number: '数値を入力してください',
    date: '日付を yyyy/MM/dd の形で入力してください',
    time: '時刻を HH:mm の形で入力してください',
    check: 'true か false を入力してください',
    list: '一覧の中から選んでください',
    between: '{0} から {1} の範囲で入力してください',
    atLeast: '{0} 以上で入力してください',
    atMost: '{0} 以下で入力してください',
    maxLength: '{0} 文字以内で入力してください',
    maxByteLength: '{0} バイト以内で入力してください(全角は2バイト)',
    pattern: '入力の形式が正しくありません',
    mask: '{0} の形で入力してください',
};

function rule(kind, extra = {}) {
    return {
        kind,
        disabled: false,
        policy: 'stay',
        required: false,
        minimum: null,
        maximum: null,
        minimumText: null,
        maximumText: null,
        maxLength: null,
        maxByteLength: null,
        pattern: null,
        items: null,
        message: null,
        ime: null,
        mask: null,
        restriction: null,
        restrictionMessage: null,
        suggest: false,
        ...extra,
    };
}

function items(...pairs) {
    return pairs.map(([value, text]) => ({ value, text: text ?? value }));
}

function error(r, text, reading) {
    return checkInput(r, text, messages, reading).error;
}

test('numbers, dates and times turn full-width characters half-width and trim surrounding spaces', () => {
    assert.equal(normalizeText('１，２３４', 'number'), '1,234');
    assert.equal(normalizeText('－５', 'number'), '-5');
    assert.equal(normalizeText('−５', 'number'), '-5');
    assert.equal(normalizeText('ー５', 'number'), 'ー5');
    assert.equal(normalizeText('ー５', 'number', JA), '-5');
    assert.equal(normalizeText('‐５', 'number', JA), '-5');
    assert.equal(normalizeText('―５', 'number', JA), '-5');
    assert.equal(normalizeText('　２０２６／１１／０１ ', 'date'), '2026/11/01');
    assert.equal(normalizeText('２０２６・１２・２４', 'date', JA), '2026/12/24');
    assert.equal(normalizeText('２０２６・１２・２４', 'date'), '2026・12・24');
    assert.equal(normalizeText('1・5', 'number'), '1・5');
    assert.equal(normalizeText('９：０５', 'time'), '9:05');
    assert.equal(normalizeText('　あ　', 'text'), '　あ　');
    assert.equal(normalizeText('  営業部 ', 'list'), '営業部');
});

test('numbers allow group separators and reject text without digits', () => {
    const number = rule('number');
    assert.equal(error(number, '1,234'), null);
    assert.equal(error(number, '１２３４'), null);
    assert.equal(error(number, '-1.5'), null);
    assert.equal(error(number, '.5'), null);
    assert.equal(error(number, 'abc'), messages.number);
    assert.equal(error(number, '5.'), messages.number);
    assert.equal(error(number, '-'), messages.number);
    assert.equal(checkInput(number, '１，２３４', messages).text, '1,234');
});

test('a required cell makes empty input an error', () => {
    assert.equal(error(rule('text', { required: true }), ''), messages.required);
    assert.equal(error(rule('number', { required: true }), '　'), messages.required);
    assert.equal(error(rule('text'), ''), null);
});

test('minimum and maximum are shown with their message text', () => {
    const range = rule('number', { minimum: '0', maximum: '100', minimumText: '0', maximumText: '100' });
    assert.equal(error(range, '100'), null);
    assert.equal(error(range, '101'), '0 から 100 の範囲で入力してください');
    assert.equal(error(rule('number', { minimum: '10', minimumText: '10' }), '9'), '10 以上で入力してください');
    assert.equal(error(rule('number', { maximum: '1000', maximumText: '1,000' }), '1,001'), '1,000 以下で入力してください');
});

test('dates accept only existing days and are compared with the range', () => {
    const date = rule('date', { minimum: '2026-01-01', minimumText: '2026/01/01' });
    assert.equal(error(date, '2026/1/5'), null);
    assert.equal(error(date, '2026-02-28'), null);
    assert.equal(error(date, '2026/02/30'), messages.date);
    assert.equal(error(date, '26/1/5'), null);
    assert.equal(error(date, '1/2/3'), messages.date);
    assert.equal(error(date, '2025/12/31'), '2026/01/01 以上で入力してください');
    assert.equal(error(rule('date'), '2024/02/29'), null);
    assert.equal(error(rule('date'), '2100/02/29'), messages.date);
});

test('times accept H:mm and H:mm:ss', () => {
    const time = rule('time', { maximum: '18:00:00', maximumText: '18:00' });
    assert.equal(error(time, '9:05'), null);
    assert.equal(error(time, '09:05:30'), null);
    assert.equal(error(time, '24:00'), messages.time);
    assert.equal(error(time, '18:01'), '18:00 以下で入力してください');
});

test('checks take true or false, and lists with items take only those items', () => {
    assert.equal(error(rule('check'), 'TRUE'), null);
    assert.equal(error(rule('check'), 'yes'), messages.check);
    const list = rule('list', { items: items(['S', '営業部'], ['G', '総務部']) });
    assert.equal(error(list, '営業部'), null);
    assert.equal(error(list, 's'), null);
    assert.equal(error(list, '開発部'), messages.list);
    assert.equal(error(rule('list'), '何でも'), null);
    assert.equal(error(rule('autocomplete', { items: items(['東京']) }), '大阪'), null);
});

test('autocomplete enters the value of a matching item, and character restrictions apply only to free text', () => {
    const city = rule('autocomplete', { items: items(['13', '東京']), restriction: SETS.fullwidth, restrictionMessage: '全角で' });
    assert.equal(error(city, '東京'), null);
    assert.equal(error(city, '13'), null);
    assert.equal(error(city, '名古屋'), null);
    assert.equal(error(city, 'abc'), '全角で');
});

test('character restrictions allow only the characters of their set', () => {
    assert.equal(isAllowed('ヤマダ　タロウ・ー', SETS.katakana), true);
    assert.equal(isAllowed('ヤマダ太郎', SETS.katakana), false);
    assert.equal(isAllowed('やまだ ー', SETS.hiragana), true);
    assert.equal(isAllowed('ﾔﾏﾀﾞ', SETS.halfwidthKatakana), true);
    assert.equal(isAllowed('ＡＢＣあ', SETS.fullwidth), true);
    assert.equal(isAllowed('ABCあ', SETS.fullwidth), false);
    assert.equal(isAllowed('ABC ﾔ', SETS.halfwidth), true);
    assert.equal(isAllowed('A1b2', SETS.alphanumeric), true);
    assert.equal(isAllowed('A1-2', SETS.alphanumeric), false);
    assert.equal(isAllowed('0123', SETS.digits), true);
    assert.equal(isAllowed('12a', SETS.digits), false);
    assert.equal(isAllowed('何でも', null), true);
});

test('character restrictions apply only to kinds entered as text', () => {
    const kana = rule('text', { restriction: SETS.katakana, restrictionMessage: 'カタカナで' });
    assert.equal(error(kana, 'ヤマダ タロウ', JA), null);
    assert.equal(checkInput(kana, 'やまだ ﾀﾛｳ', messages, JA).text, 'やまだ ﾀﾛｳ');
    assert.equal(error(kana, 'やまだ', JA), 'カタカナで');
    assert.equal(error(kana, '山田', JA), 'カタカナで');
    assert.equal(error(rule('text', { restriction: SETS.digits, restrictionMessage: '数字で' }), '12a', JA), '数字で');
    assert.equal(error(rule('number', { restriction: SETS.katakana, restrictionMessage: 'カタカナで' }), '12', JA), null);
});

test('masks let separators be left out or typed differently, and shape the text before the commit', () => {
    assert.equal(formatMask('1234567', '999-9999'), '123-4567');
    assert.equal(formatMask('123-4567', '999-9999'), '123-4567');
    assert.equal(formatMask('123 4567', '999-9999'), '123-4567');
    assert.equal(formatMask('12-34567', '999-9999'), null);
    assert.equal(formatMask('123456', '999-9999'), null);
    assert.equal(formatMask('12345678', '999-9999'), null);
    assert.equal(formatMask('AB12', 'AA-99'), 'AB-12');
    assert.equal(formatMask('A112', 'X-X99'), 'A-112');
    assert.equal(formatMask('12', '\\9-99'), '9-12');
    assert.equal(maskDisplay('\\9-99'), '9-99');
    const zip = rule('mask', { mask: '999-9999' });
    assert.equal(prepareText(zip, '１２３４５６７'), '123-4567');
    assert.equal(error(zip, '１２３ー４５６７', JA), null);
    assert.equal(error(zip, '１２３－４５６７'), null);
    assert.equal(error(zip, '12345'), '999-9999 の形で入力してください');
    assert.equal(error(zip, ''), null);
});

test('suggestions put prefix matches first and other matches after them', () => {
    const list = items(['G', '総務部'], ['S', '営業部'], ['D', '開発部'], ['E', '営業推進部']);
    assert.deepEqual(filterItems(list, '営業').map(x => x.text), ['営業部', '営業推進部']);
    assert.deepEqual(filterItems(list, '部').map(x => x.text), ['総務部', '営業部', '開発部', '営業推進部']);
    assert.deepEqual(filterItems(list, 'd').map(x => x.text), ['開発部']);
    assert.deepEqual(filterItems(list, '推進').map(x => x.text), ['営業推進部']);
    assert.equal(filterItems(list, '  ').length, 4);
    const cities = items(['tokyo', 'Tokyo'], ['ＯＳＡＫＡ', 'ＯＳＡＫＡ']);
    assert.deepEqual(filterItems(cities, 'ｔｏｋ').map(x => x.value), ['tokyo']);
    assert.deepEqual(filterItems(cities, 'osa').map(x => x.value), ['ＯＳＡＫＡ']);
});

test('PageUp and PageDown count by row heights', () => {
    const tops = [0, 24, 48, 248, 272, 296, 320];
    assert.equal(pageTarget(tops, 0, 1, 100), 2);
    assert.equal(pageTarget(tops, 2, 1, 100), 3);
    assert.equal(pageTarget(tops, 5, -1, 100), 2);
    assert.equal(pageTarget(tops, 4, 1, 1000), 5);
    assert.equal(pageTarget(tops, 0, -1, 100), 0);
});

test('calendar dates are parsed, formatted and stepped', () => {
    assert.deepEqual(parseDate('２０２６／１１／５'), { y: 2026, m: 11, d: 5 });
    assert.deepEqual(parseDate('２０２６・１２・２４', JA), { y: 2026, m: 12, d: 24 });
    assert.equal(parseDate('2026/02/30'), null);
    assert.equal(formatDate({ y: 2026, m: 3, d: 1 }), '2026/03/01');
    assert.deepEqual(addDays({ y: 2026, m: 3, d: 1 }, -1), { y: 2026, m: 2, d: 28 });
    assert.deepEqual(addDays({ y: 2024, m: 2, d: 28 }, 1), { y: 2024, m: 2, d: 29 });
    assert.deepEqual(addDays({ y: 2026, m: 12, d: 31 }, 1), { y: 2027, m: 1, d: 1 });
    assert.deepEqual(addMonths({ y: 2026, m: 1, d: 31 }, 1), { y: 2026, m: 2, d: 28 });
    assert.deepEqual(addMonths({ y: 2026, m: 1, d: 15 }, -1), { y: 2025, m: 12, d: 15 });
    const grid = monthGrid(2026, 11);
    assert.equal(grid.length, 42);
    assert.deepEqual(grid[0], { y: 2026, m: 11, d: 1 });
    assert.deepEqual(grid[41], { y: 2026, m: 12, d: 12 });
    assert.deepEqual(monthGrid(2026, 10)[0], { y: 2026, m: 9, d: 27 });
});

test('lengths count a surrogate pair as one character, and byte lengths count full-width characters as two', () => {
    assert.equal(error(rule('text', { maxLength: 3 }), '𠮷野家'), null);
    assert.equal(error(rule('text', { maxLength: 2 }), '𠮷野家'), '2 文字以内で入力してください');
    assert.equal(countBytes('ｱｲｳ'), 3);
    assert.equal(countBytes('あい'), 4);
    assert.equal(countBytes('A1あ'), 4);
    assert.equal(error(rule('text', { maxByteLength: 4 }), 'あいう'), '4 バイト以内で入力してください(全角は2バイト)');
});

test('patterns must match the whole text, and an unusable pattern checks nothing', () => {
    const zip = rule('text', { pattern: '[0-9]{3}-[0-9]{4}' });
    assert.equal(error(zip, '123-4567'), null);
    assert.equal(error(zip, 'x123-4567'), messages.pattern);
    assert.equal(error(rule('text', { pattern: '(' }), 'abc'), null);
});

test('the error message of the cell settings is used when there is one', () => {
    const custom = rule('number', { message: '0〜100 で入力してください', minimum: '0', maximum: '100', minimumText: '0', maximumText: '100' });
    assert.equal(error(custom, 'abc'), '0〜100 で入力してください');
    assert.equal(error(custom, '200'), '0〜100 で入力してください');
});

const sample = {
    rows: 4,
    cols: 4,
    isDisabled: (r, c) => c === 0 || c === 3 || r === 2,
};

function walk(start, dir, skip, wrap) {
    const cells = [];
    let current = start;
    for (;;) {
        current = findNext(sample, current.r, current.c, dir, skip, wrap);
        if (!current) {
            return cells;
        }
        cells.push(`${current.r},${current.c}`);
    }
}

test('moving right past the end of a row goes on in Z order, skipping disabled cells and subtotal rows', () => {
    assert.deepEqual(walk({ r: 0, c: 1 }, 'right', true, true), ['0,2', '1,1', '1,2', '3,1', '3,2']);
    assert.deepEqual(walk({ r: 3, c: 2 }, 'left', true, true), ['3,1', '1,2', '1,1', '0,2', '0,1']);
});

test('moving down goes to the next input cell in the column and stops at its end', () => {
    assert.deepEqual(walk({ r: 0, c: 1 }, 'down', true, false), ['1,1', '3,1']);
});

test('without skipping, moves stop on disabled cells and at the edge of the table', () => {
    assert.deepEqual(findNext(sample, 1, 1, 'down', false, false), { r: 2, c: 1 });
    assert.equal(findNext(sample, 0, 3, 'right', false, false), null);
    assert.equal(findNext(sample, 0, 0, 'up', false, false), null);
});

test('the first input cell is the first enabled cell, or the top left when none is enabled', () => {
    assert.deepEqual(firstInputCell(sample), { r: 0, c: 1 });
    assert.deepEqual(firstInputCell({ rows: 2, cols: 2, isDisabled: () => true }), { r: 0, c: 0 });
});

test('cell positions come from the headers and the height of each row and the width of each column', () => {
    const map = new SheetMap({
        rows: 3,
        columns: 2,
        headerHeight: 24,
        rowHeaderWidth: 40,
        columnWidths: [100, 120],
        defaultRowHeight: 24,
        customRowHeights: [1, 40],
        rules: [rule('text')],
        patterns: [[0, 0]],
        rowPatterns: [0, 0, 0],
        messages,
    });
    assert.deepEqual(map.cellRect(0, 0), { x: 40, y: 24, w: 100, h: 24 });
    assert.deepEqual(map.cellRect(1, 1), { x: 140, y: 48, w: 120, h: 40 });
    assert.deepEqual(map.cellRect(2, 0), { x: 40, y: 88, w: 100, h: 24 });
    assert.equal(map.rule(2, 1).kind, 'text');
});

test('a paste into one cell takes only the first cell of a table, handling the trailing line break and quotes that Excel writes', () => {
    assert.equal(clipboardCell('営業部\r\n'), '営業部');
    assert.equal(clipboardCell('1,234'), '1,234');
    assert.equal(clipboardCell('a\tb\r\nc\td\r\n'), 'a');
    assert.equal(clipboardCell('"1行目\n2行目"\tb\r\n'), '1行目2行目');
    assert.equal(clipboardCell('"引用符""あり"'), '引用符"あり');
    assert.equal(clipboardCell('"閉じていない'), '"閉じていない');
    assert.equal(clipboardCell('"途中"の引用符\tb'), '"途中"の引用符');
    assert.equal(clipboardCell(''), '');
    assert.equal(clipboardCell('\r\n'), '');
});

test('clipboard tables are split the way Excel writes TSV, as in C# ClipboardFormat', () => {
    assert.deepEqual(parseClipboard('a\tb\r\nc\td\r\n'), [['a', 'b'], ['c', 'd']]);
    assert.deepEqual(parseClipboard('営業部\r\n'), [['営業部']]);
    assert.deepEqual(parseClipboard('"1行目\n2行目"\tb\r\n'), [['1行目\n2行目', 'b']]);
    assert.deepEqual(parseClipboard('"引用符""あり"'), [['引用符"あり']]);
    assert.deepEqual(parseClipboard('"閉じていない'), [['"閉じていない']]);
    assert.deepEqual(parseClipboard('"途中"の引用符\tb'), [['"途中"の引用符', 'b']]);
    assert.deepEqual(parseClipboard('\r\n'), [['']]);
    assert.deepEqual(parseClipboard('a\t\tc'), [['a', '', 'c']]);
    assert.deepEqual(parseClipboard('a\n\nb'), [['a'], [''], ['b']]);
    assert.deepEqual(parseClipboard('a\rb'), [['a'], ['b']]);
    assert.deepEqual(parseClipboard(''), []);
});

test('TSV and HTML tables are written as in C# ClipboardFormat', () => {
    const rows = [['a', 'b'], ['c\td', '"x'], ['中"の引用符', '']];
    const tsv = toTsv(rows);
    assert.equal(tsv, 'a\tb\r\n"c\td"\t"""x"\r\n中"の引用符\t\r\n');
    assert.deepEqual(parseClipboard(tsv), rows);
    const style = '<style>.istext{mso-number-format:"\\@";}br{mso-data-placement:same-cell;}</style>';
    assert.equal(
        toHtml([['<a>&\'"', '0123']], (r, c) => c === 1),
        `${style}<table><tr><td>&lt;a&gt;&amp;&#39;&quot;</td><td class="istext" style='mso-number-format:"\\@"'>0123</td></tr></table>`);
    assert.equal(toHtml([['1行目\n2行目']], () => false), `${style}<table><tr><td>1行目<br>2行目</td></tr></table>`);
});

test('Enter and Tab in a range go on to the next column or row at its edge and wrap from the last cell to the first, skipping disabled cells', () => {
    const grid = { rows: 5, cols: 5, isDisabled: (r, c) => r === 1 && c === 1 };
    const range = { top: 0, left: 0, bottom: 1, right: 1 };
    assert.deepEqual(nextInRange(grid, range, 0, 0, 'col', false, true), { r: 1, c: 0 });
    assert.deepEqual(nextInRange(grid, range, 1, 0, 'col', false, true), { r: 0, c: 1 });
    assert.deepEqual(nextInRange(grid, range, 0, 1, 'col', false, true), { r: 0, c: 0 });
    assert.deepEqual(nextInRange(grid, range, 0, 1, 'col', false, false), { r: 1, c: 1 });
    assert.deepEqual(nextInRange(grid, range, 0, 1, 'row', false, true), { r: 1, c: 0 });
    assert.deepEqual(nextInRange(grid, range, 0, 0, 'row', true, true), { r: 1, c: 0 });
    const blocked = { rows: 2, cols: 2, isDisabled: (r, c) => !(r === 0 && c === 0) };
    assert.equal(nextInRange(blocked, range, 0, 0, 'row', false, true), null);
});

test('positions map to row and column numbers, clamped at the edges, and two corners make a range', () => {
    const totals = [0, 24, 48, 88];
    assert.equal(indexAt(totals, -5), 0);
    assert.equal(indexAt(totals, 0), 0);
    assert.equal(indexAt(totals, 23.9), 0);
    assert.equal(indexAt(totals, 24), 1);
    assert.equal(indexAt(totals, 87), 2);
    assert.equal(indexAt(totals, 500), 2);
    assert.equal(indexAt([0], 10), 0);
    assert.deepEqual(rangeOf({ r: 3, c: 1 }, { r: 1, c: 4 }), { top: 1, left: 1, bottom: 3, right: 4 });
});

test('line breaks become LF in multiline text and are removed in other cells, as in C# NormalizeLines', () => {
    assert.equal(normalizeLines('a\r\nb\rc\nd', true), 'a\nb\nc\nd');
    assert.equal(normalizeLines('a\r\nb\rc\nd', false), 'abcd');
    assert.equal(prepareText(rule('text', { multiline: true }), '1行目\r\n2行目'), '1行目\n2行目');
    assert.equal(prepareText(rule('text'), '1行目\r\n2行目'), '1行目2行目');
    assert.equal(clipboardCell('"a\r\nb"\tc\r\n', true), 'a\nb');
    assert.equal(clipboardCell('"a\r\nb"\tc\r\n'), 'ab');
});

const menuMessages = {
    menuCut: '切り取り',
    menuCopy: 'コピー',
    menuPaste: '貼り付け',
    menuInsertRowsAbove: '上に {0} 行挿入',
    menuInsertRowsBelow: '下に {0} 行挿入',
    menuDeleteRows: '{0} 行を削除',
    menuInsertColumnsLeft: '左に {0} 列挿入',
    menuInsertColumnsRight: '右に {0} 列挿入',
    menuDeleteColumns: '{0} 列を削除',
};

const allowAll = { allowInsertRows: true, allowDeleteRows: true, allowInsertColumns: true, allowDeleteColumns: true };

test('the context menu is for rows or columns when whole rows or columns are selected, and for cells otherwise', () => {
    const size = { rows: 10, cols: 4 };
    assert.equal(menuKind({ top: 2, left: 1, bottom: 2, right: 1 }, size), 'cell');
    assert.equal(menuKind({ top: 2, left: 0, bottom: 3, right: 3 }, size), 'row');
    assert.equal(menuKind({ top: 0, left: 1, bottom: 9, right: 2 }, size), 'column');
    assert.equal(menuKind({ top: 0, left: 0, bottom: 9, right: 3 }, size), 'cell');
    assert.equal(menuKind({ top: 1, left: 1, bottom: 3, right: 2 }, size), 'cell');
});

test('the context menu shows the clipboard items and the allowed row and column inserts and deletes, with the selected count', () => {
    const size = { rows: 10, cols: 4 };
    const range = { top: 2, left: 1, bottom: 4, right: 1 };
    const actions = entries => entries.map(x => x?.action ?? '-');

    const none = menuEntries('cell', range, size, {}, true, menuMessages);
    assert.deepEqual(actions(none), ['cut', 'copy', 'paste']);

    const all = menuEntries('cell', range, size, allowAll, true, menuMessages);
    assert.deepEqual(actions(all), ['cut', 'copy', 'paste', '-', 'insertRowsAbove', 'insertRowsBelow', 'deleteRows', '-', 'insertColumnsLeft', 'insertColumnsRight', 'deleteColumns']);
    assert.equal(all[4].label, '上に 3 行挿入');
    assert.equal(all[6].label, '3 行を削除');
    assert.equal(all[8].label, '左に 1 列挿入');
    assert.ok(all[1].key.endsWith('C'));

    assert.deepEqual(actions(menuEntries('row', range, size, allowAll, true, menuMessages)).slice(3), ['-', 'insertRowsAbove', 'insertRowsBelow', 'deleteRows']);
    assert.deepEqual(actions(menuEntries('column', range, size, { allowInsertColumns: true }, true, menuMessages)).slice(3), ['-', 'insertColumnsLeft', 'insertColumnsRight']);
});

test('cut and paste are off for a range without input cells, and delete is off when every row or column is selected', () => {
    const size = { rows: 3, cols: 2 };
    const entries = menuEntries('cell', { top: 0, left: 0, bottom: 2, right: 0 }, size, allowAll, false, menuMessages);
    const disabled = entries.filter(x => x?.disabled).map(x => x.action);
    assert.deepEqual(disabled, ['cut', 'paste', 'deleteRows']);
});

test('menu keys skip separators and disabled items and wrap around at the ends', () => {
    const entries = [{ action: 'cut', disabled: true }, { action: 'copy' }, { action: 'paste', disabled: true }, null, { action: 'insertRowsAbove' }];
    assert.equal(nextMenuIndex(entries, -1, 1), 1);
    assert.equal(nextMenuIndex(entries, -1, -1), 4);
    assert.equal(nextMenuIndex(entries, 1, 1), 4);
    assert.equal(nextMenuIndex(entries, 4, 1), 1);
    assert.equal(nextMenuIndex(entries, 1, -1), 4);
    assert.equal(nextMenuIndex([{ action: 'cut', disabled: true }, null], -1, 1), -1);
});

test('with frozen rows and columns, each cell belongs to a pane', () => {
    assert.equal(paneOf(0, 0, 1, 1), 'tl');
    assert.equal(paneOf(0, 5, 1, 1), 't');
    assert.equal(paneOf(3, 0, 1, 1), 'l');
    assert.equal(paneOf(3, 5, 1, 1), 'main');
    assert.equal(paneOf(0, 0, 0, 0), 'main');
});

test('a selection is split at the frozen edges and framed only on its outer sides', () => {
    const pieces = selectionPieces({ top: 0, left: 1, bottom: 4, right: 3 }, 1, 2);
    assert.deepEqual(pieces.map(x => [x.pane, x.top, x.left, x.bottom, x.right]), [
        ['tl', 0, 1, 0, 1],
        ['t', 0, 2, 0, 3],
        ['l', 1, 1, 4, 1],
        ['main', 1, 2, 4, 3],
    ]);
    assert.deepEqual(pieces[0].edges, { top: true, left: true, bottom: false, right: false });
    assert.deepEqual(pieces[3].edges, { top: false, left: false, bottom: true, right: true });

    const single = selectionPieces({ top: 2, left: 2, bottom: 3, right: 3 }, 0, 0);
    assert.deepEqual(single.map(x => x.pane), ['main']);
    assert.deepEqual(single[0].edges, { top: true, left: true, bottom: true, right: true });

    assert.deepEqual(selectionPieces({ top: 0, left: 3, bottom: 0, right: 4 }, 2, 1).map(x => x.pane), ['t']);
});

test('positions over frozen rows and columns map to numbers where they stick', () => {
    const totals = [0, 24, 48, 72, 96];
    assert.equal(frozenIndex(totals, 2, 10, 500), 0);
    assert.equal(frozenIndex(totals, 2, 30, 500), 1);
    assert.equal(frozenIndex(totals, 2, -5, 500), 0);
    assert.equal(frozenIndex(totals, 2, 60, 80), 3);
    assert.equal(frozenIndex(totals, 2, 60, 10), 2);
    assert.equal(frozenIndex(totals, 2, 60, 999), 3);
    assert.equal(frozenIndex(totals, 0, 10, 50), 2);
});

function mergedSheet() {
    return new SheetMap({
        rows: 7,
        columns: 4,
        headerHeight: 24,
        rowHeaderWidth: 40,
        columnWidths: [100, 100, 100, 100],
        defaultRowHeight: 24,
        customRowHeights: [],
        rules: [rule('text'), rule('label', { disabled: true })],
        patterns: [[1, 0, 0, 0], [1, 1, 1, 0], [0, 0, 0, 0]],
        rowPatterns: [0, 0, 1, 0, 0, 0, 2],
        messages,
        merges: [0, 0, 0, 3, 1, 1, 1, 3, 2, 0, 2, 2, 3, 0, 5, 0, 3, 1, 5, 3],
    });
}

test('a merged range is found from any of its cells, only its top-left cell takes input, and its position is the whole range', () => {
    const map = mergedSheet();
    assert.deepEqual(map.mergeAt(5, 2), { top: 3, left: 1, bottom: 5, right: 3 });
    assert.equal(map.mergeAt(6, 0), null);
    assert.equal(map.isCovered(3, 1), false);
    assert.equal(map.isCovered(4, 2), true);
    assert.equal(map.isDisabled(4, 2), true);
    assert.equal(map.isDisabled(3, 1), false);
    assert.deepEqual(map.anchorOf(1, 3), { r: 1, c: 1 });
    assert.deepEqual(map.cellRect(4, 2), { x: 140, y: 96, w: 300, h: 72 });
    assert.deepEqual(map.cellBox(4, 2), { x: 240, y: 120, w: 100, h: 24 });
});

test('a selection grows to cover merged ranges it partly overlaps', () => {
    const map = mergedSheet();
    assert.deepEqual(map.expandRange({ top: 1, left: 2, bottom: 2, right: 2 }), { top: 1, left: 0, bottom: 2, right: 3 });
    assert.deepEqual(map.expandRange({ top: 6, left: 0, bottom: 6, right: 1 }), { top: 6, left: 0, bottom: 6, right: 1 });
    assert.deepEqual(map.expandRange({ top: 5, left: 0, bottom: 5, right: 0 }), { top: 3, left: 0, bottom: 5, right: 0 });
    assert.deepEqual(map.expandRange({ top: 5, left: 0, bottom: 5, right: 1 }), { top: 3, left: 0, bottom: 5, right: 3 });
});

test('a merged range moves as one cell, entered at its top left and left from its edge', () => {
    const map = mergedSheet();
    assert.deepEqual(findNext(map, 1, 1, 'down', true, false), { r: 3, c: 1 });
    assert.deepEqual(findNext(map, 3, 1, 'down', true, false), { r: 6, c: 1 });
    assert.deepEqual(findNext(map, 1, 2, 'down', false, false), { r: 2, c: 0 });
    assert.deepEqual(findNext(map, 2, 0, 'down', false, false), { r: 3, c: 0 });
    assert.deepEqual(findNext(map, 1, 1, 'right', true, true), { r: 2, c: 3 });
    assert.deepEqual(findNext(map, 2, 3, 'right', true, true), { r: 3, c: 1 });
    assert.deepEqual(findNext(map, 3, 1, 'right', true, true), { r: 6, c: 0 });
    assert.deepEqual(findNext(map, 3, 0, 'right', false, false), { r: 3, c: 1 });
    assert.equal(findNext(map, 3, 1, 'right', false, false), null);
});

test('Enter and Tab in a range do not stop on merged cells other than the top left', () => {
    const map = mergedSheet();
    const range = { top: 1, left: 1, bottom: 3, right: 3 };
    assert.deepEqual(nextInRange(map, range, 1, 1, 'row', false, false), { r: 2, c: 3 });
    assert.deepEqual(nextInRange(map, range, 2, 3, 'row', false, false), { r: 3, c: 1 });
});

test('quick date input puts a month and day without a year in this year, and accepts two-digit years, digits only and kanji forms', () => {
    const base = { y: 2026, m: 11, d: 2 };
    const cases = [
        ['11/5', 2026, 11, 5], ['1-5', 2026, 1, 5], ['11.5', 2026, 11, 5],
        ['25/11/5', 2025, 11, 5], ['29-1-1', 2029, 1, 1], ['30.1.1', 1930, 1, 1],
        ['1105', 2026, 11, 5], ['251105', 2025, 11, 5], ['20251105', 2025, 11, 5],
        ['11月5日', 2026, 11, 5], ['2025年11月5日', 2025, 11, 5], ['25年1月15日', 2025, 1, 15],
    ];
    for (const [text, y, m, d] of cases) {
        assert.deepEqual(matchDate(text, base, JA), { y, m, d }, text);
    }
    for (const text of ['5', '115', '1/2/3', '2/29', '1305', '11月5', '2025/11']) {
        assert.equal(matchDate(text, base, JA), null, text);
    }
    assert.equal(parseDate('１１・５', JA).m, 11);
    assert.equal(checkInput(rule('date'), '１１月５日', messages, JA).error, null);
    assert.equal(checkInput(rule('date'), '5', messages).error, messages.date);
});

function groupedSheet() {
    return new SheetMap({
        rows: 4,
        columns: 4,
        headerHeight: 24,
        rowHeaderWidth: 40,
        columnWidths: [100, 100, 100, 100],
        defaultRowHeight: 24,
        customRowHeights: [],
        rules: [rule('text'), rule('text', { group: '見出し' }), rule('label', { disabled: true }), rule('text', { group: '備考' })],
        patterns: [[2, 1, 1, 2], [0, 0, 0, 3], [0, 0, 0, 2]],
        rowPatterns: [0, 1, 1, 2],
        messages,
    });
}

test('inside a move group, moves wrap as in a selected range', () => {
    const map = groupedSheet();
    assert.deepEqual(map.groupBounds.get('見出し'), { top: 0, left: 1, bottom: 0, right: 2 });
    assert.deepEqual(map.groupBounds.get('備考'), { top: 1, left: 3, bottom: 2, right: 3 });
    assert.equal(map.groupAt(1, 0), null);

    assert.deepEqual(nextInGroup(map, '見出し', 0, 1, 'row', false, true), { r: 0, c: 2 });
    assert.deepEqual(nextInGroup(map, '見出し', 0, 2, 'row', false, true), { r: 0, c: 1 });
    assert.deepEqual(nextInGroup(map, '見出し', 0, 1, 'row', true, true), { r: 0, c: 2 });
    assert.deepEqual(nextInGroup(map, '備考', 1, 3, 'col', false, true), { r: 2, c: 3 });
    assert.deepEqual(nextInGroup(map, '備考', 2, 3, 'col', false, true), { r: 1, c: 3 });
    assert.equal(nextInGroup(map, 'なし', 0, 0, 'row', false, true), null);
});

test('from a cell without a move group, moves pass over grouped cells as before', () => {
    const map = groupedSheet();
    const free = (r, c) => map.groupAt(r, c) === null;
    assert.deepEqual(findNext(map, 1, 2, 'right', true, true, free), { r: 2, c: 0 });
    assert.deepEqual(findNext(map, 1, 2, 'right', true, true), { r: 1, c: 3 });
    assert.equal(findNext(map, 1, 1, 'up', true, false, free), null);
    assert.equal(findNext(map, 3, 2, 'right', true, true, free), null);
});

test('the calendar follows the culture for the first day of the week and the year and month text', () => {
    assert.deepEqual(monthGrid(2026, 11, 1)[0], { y: 2026, m: 10, d: 26 });
    assert.deepEqual(monthGrid(2026, 11)[0], { y: 2026, m: 11, d: 1 });
    const names = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
    const calendar = { monthNames: names, abbreviatedMonthNames: names.map(x => x.slice(0, 3)), era: 'A.D.' };
    assert.equal(formatYearMonth('yyyy年M月', 2026, 11, calendar), '2026年11月');
    assert.equal(formatYearMonth('MMMM yyyy', 2026, 11, calendar), 'November 2026');
    assert.equal(formatYearMonth("yyyy'/'MM", 2026, 3, calendar), '2026/03');
    assert.equal(formatYearMonth('MMM yyyy g', 2026, 11, calendar), 'Nov 2026 A.D.');
});

test('numbers follow the sheet culture for the decimal and group separators, and dates for the field order', () => {
    const base = { y: 2026, m: 11, d: 2 };
    assert.deepEqual(matchDate('11/5/2025', base, US), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('11/5/25', base, US), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('11/5', base, US), { y: 2026, m: 11, d: 5 });
    assert.deepEqual(matchDate('11052025', base, US), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('2025-11-05', base, US), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('05.11.2025', base, DE), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('5.11', base, DE), { y: 2026, m: 11, d: 5 });
    assert.deepEqual(matchDate('0511', base, DE), { y: 2026, m: 11, d: 5 });
    assert.equal(matchDate('11月5日', base, US), null);
    assert.equal(parseDate('2025・11・5', US), null);
    assert.equal(parseNumber('1,234.5', US), 1234.5);
    assert.equal(parseNumber('1.234,5', DE), 1234.5);
    assert.equal(parseNumber('1,5', DE), 1.5);
    assert.equal(parseNumber('1.5', DE), 15);
    assert.equal(parseNumber(',5', DE), 0.5);
    assert.equal(parseNumber('5,', DE), null);
    assert.equal(parseNumber('1 234,5', compileReading({ decimal: ',', group: '\u202f', order: 'dmy', editDate: 'dd/MM/yyyy' })), 1234.5);
    assert.equal(checkInput(rule('number'), '1.234,5', messages, DE).error, null);
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }, US), '11/5/2025');
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }, DE), '05.11.2025');
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }), '2025/11/05');
});

test('keys are written in the order Ctrl, Alt, Shift, with the key in upper case and a space as SPACE', () => {
    assert.equal(comboOf({ key: 'd', ctrl: true, alt: false, shift: false }), 'Ctrl+D');
    assert.equal(comboOf({ key: 'Enter', ctrl: true, alt: false, shift: true }), 'Ctrl+Shift+ENTER');
    assert.equal(comboOf({ key: 'F4', ctrl: false, alt: false, shift: false }), 'F4');
    assert.equal(comboOf({ key: ' ', ctrl: false, alt: true, shift: false }), 'Alt+SPACE');
    assert.equal(comboOf({ key: 'Pause', ctrl: true, alt: false, shift: false }), 'Ctrl+PAUSE');
    assert.equal(comboOf({ key: 'F13', ctrl: false, alt: false, shift: false }), 'F13');
    assert.equal(comboOf({ key: 'ß', ctrl: true, alt: false, shift: false }), 'Ctrl+ß');
    assert.equal(comboOf({ key: '\u0131', ctrl: true, alt: false, shift: false }), 'Ctrl+I');
});

test('the context menu leaves out hidden built-in items and adds the items of the app at the end', () => {
    const range = { top: 0, left: 0, bottom: 0, right: 0 };
    const size = { rows: 5, cols: 5 };
    const labels = { menuCut: 'Cut', menuCopy: 'Copy', menuPaste: 'Paste', menuInsertRowsAbove: '{0}', menuInsertRowsBelow: '{0}', menuDeleteRows: '{0}', menuInsertColumnsLeft: '{0}', menuInsertColumnsRight: '{0}', menuDeleteColumns: '{0}' };
    const options = {
        allowInsertRows: true,
        allowDeleteRows: true,
        hiddenMenuItems: ['cut', 'deleteRows'],
        menuItems: [{ value: 'note', text: 'Note' }],
    };
    const entries = menuEntries('cell', range, size, options, true, labels);
    assert.deepEqual(entries.map(x => x ? x.action : '-'), ['copy', 'paste', '-', 'insertRowsAbove', 'insertRowsBelow', '-', 'app']);
    assert.equal(entries.at(-1).name, 'note');
    assert.equal(entries.at(-1).label, 'Note');

    const onlyApp = menuEntries('cell', range, size, { hiddenMenuItems: ['cut', 'copy', 'paste'], menuItems: [{ value: 'a', text: 'A' }] }, true, labels);
    assert.deepEqual(onlyApp.map(x => x ? x.action : '-'), ['app']);
});

test('Japanese eras are read by name, short name or initial, and years past the end of an era are accepted', () => {
    const base = { y: 2026, m: 11, d: 2 };
    const cases = [
        ['令和7年11月5日', 2025, 11, 5], ['令和元年5月1日', 2019, 5, 1], ['令和7/11/5', 2025, 11, 5], ['令和7.11.5', 2025, 11, 5],
        ['R7.11.5', 2025, 11, 5], ['r7/11/5', 2025, 11, 5], ['H31.4.30', 2019, 4, 30], ['H31.5.1', 2019, 5, 1],
        ['H32.1.1', 2020, 1, 1], ['平成32年1月1日', 2020, 1, 1], ['S64.1.7', 1989, 1, 7], ['M45.7.29', 1912, 7, 29],
        ['令7年11月5日', 2025, 11, 5], ['令元年5月1日', 2019, 5, 1], ['令和元/5/1', 2019, 5, 1], ['R元.5.1', 2019, 5, 1],
    ];
    for (const [text, y, m, d] of cases) {
        assert.deepEqual(matchDate(text, base, JA), { y, m, d }, text);
    }
    for (const text of ['令和1年4月30日', '令1年4月30日', 'R0.1.1', 'X7.1.1', 'R071105', '令和7年11月5', 'R7.13.1', '令和100年1月1日']) {
        assert.equal(matchDate(text, base, JA), null, text);
    }

    assert.equal(matchDate('R7.11.5', base, US), null);
    assert.deepEqual(parseDate('Ｒ７．１１．５', JA), { y: 2025, m: 11, d: 5 });
});

test('a changed Japanese edit format still reads what it writes, first years included', () => {
    const era = compileReading({ ...LANGUAGES['ja-JP'], editDate: 'ggge年M月d日' });
    const base = { y: 2026, m: 11, d: 2 };
    assert.equal(formatDate({ y: 2019, m: 5, d: 1 }, era), '令和元年5月1日');
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }, era), '令和7年11月5日');
    assert.equal(formatDate({ y: 1800, m: 1, d: 1 }, era), '1800/01/01');
    assert.deepEqual(matchDate(formatDate({ y: 2019, m: 5, d: 1 }, era), base, era), { y: 2019, m: 5, d: 1 });
});

test('other languages read and write with data of the same form alone', () => {
    const base = { y: 2026, m: 11, d: 2 };
    assert.deepEqual(matchDate('民國114年11月5日', base, TW), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('民國元年1月1日', base, TW), { y: 1912, m: 1, d: 1 });
    assert.deepEqual(matchDate('114年11月5日', base, TW), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('2025/11/5', base, TW), { y: 2025, m: 11, d: 5 });
    assert.equal(matchDate('令和7年11月5日', base, TW), null);

    assert.deepEqual(parseDate('๕/๑๑/๒๕๖๘', TH), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('5/11/2568 พ.ศ.', base, TH), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('พ.ศ. 2568/11/5', base, TH), { y: 2025, m: 11, d: 5 });
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }, TH), '5/11/2568');
    assert.deepEqual(matchDate(formatDate({ y: 2030, m: 1, d: 2 }, TH), base, TH), { y: 2030, m: 1, d: 2 });

    assert.deepEqual(matchDate('2025년 11월 5일', base, KO), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('2025년11월5일', base, KO), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('11월 5일', base, KO), { y: 2026, m: 11, d: 5 });
    assert.equal(matchDate('2025年11月5日', base, KO), null);

    assert.equal(normalizeText('๑๒๓', 'number', TH), '123');
    assert.equal(normalizeText('١٢٣', 'number', AR), '123');
    assert.equal(normalizeText('۴۵۶', 'number', AR), '456');
});

test('culture signs, separators and control characters are read', () => {
    assert.equal(normalizeText('\u061C-5', 'number'), '-5');
    assert.equal(normalizeText('\u200E-5', 'number'), '-5');
    assert.equal(normalizeText('\u200F-5', 'number'), '-5');
    assert.equal(normalizeText('\u200E\u22125', 'number'), '-5');
    assert.equal(normalizeText('5\u200F/11\u200F/2025', 'date'), '5/11/2025');

    const base = { y: 2026, m: 11, d: 2 };
    const dmy = compileReading({ decimal: ',', group: '.', time: '.', order: 'dmy', editDate: 'dd.MM.yyyy' });
    assert.deepEqual(matchDate('5. 11. 2025.', base, dmy), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('5.11.2025.', base, dmy), { y: 2025, m: 11, d: 5 });
    assert.deepEqual(matchDate('5.11.', base, dmy), { y: 2026, m: 11, d: 5 });
    assert.equal(checkInput(rule('time'), '12.30', messages, dmy).error, null);
    assert.equal(checkInput(rule('time'), '12:30', messages, dmy).error, null);
    assert.equal(checkInput(rule('time'), '12-30', messages, dmy).error, messages.time);

    const sameSeparators = compileReading({ decimal: ',', group: '' });
    assert.equal(parseNumber('1234,5', sameSeparators), 1234.5);
    assert.equal(parseNumber('1,234,5', sameSeparators), null);
});

test('trimming removes the same spaces as C#', () => {
    assert.equal(normalizeText('\u00851\u0085', 'number'), '1');
    assert.equal(normalizeText('\uFEFF1\uFEFF', 'number'), '1');
    assert.equal(normalizeText('\u0085営業部\uFEFF', 'list'), '営業部');
});

test('half-width means ASCII and the Unicode half-width forms', () => {
    assert.equal(countBytes('ﾡￂ'), 2);
    assert.equal(countBytes('￨'), 1);
    assert.equal(countBytes('￠'), 2);
    assert.equal(isAllowed('ﾡￂ', SETS.halfwidth), true);
    assert.equal(isAllowed('ﾡￂ', SETS.fullwidth), false);
    assert.equal(isAllowed('é', SETS.fullwidth), true);
});

test('the fill handle range reaches the cell it is dragged to', () => {
    const source = { top: 2, left: 1, bottom: 3, right: 2 };
    assert.deepEqual(fillTarget(source, { r: 6, c: 2 }), { top: 2, left: 1, bottom: 6, right: 2 });
    assert.deepEqual(fillTarget(source, { r: 0, c: 1 }), { top: 0, left: 1, bottom: 3, right: 2 });
    assert.deepEqual(fillTarget(source, { r: 4, c: 6 }), { top: 2, left: 1, bottom: 3, right: 6 });
    assert.deepEqual(fillTarget(source, { r: 3, c: 0 }), { top: 2, left: 0, bottom: 3, right: 2 });
    assert.deepEqual(fillTarget(source, { r: 5, c: 4 }), { top: 2, left: 1, bottom: 5, right: 2 });
    assert.equal(fillTarget(source, { r: 3, c: 1 }), null);
});

test('a cell with a Corrector checks only the type', () => {
    const limited = rule('number', { maximum: '1000', required: true, corrects: true });
    assert.equal(error(limited, '2000'), null);
    assert.equal(error(limited, ''), null);
    assert.equal(error(limited, 'abc'), messages.number);
    assert.equal(error(rule('number', { maximum: '1000' }), '2000') !== null, true);
    assert.equal(error(rule('list', { items: items(['A']), corrects: true }), 'a-1'), messages.list);
    assert.equal(error(rule('list', { items: items(['A']), corrects: true }), 'a'), null);
});

test('long runs of spaces or dots are read in linear time', () => {
    const spaces = `1${' '.repeat(200000)}2`;
    const dots = `1${'.'.repeat(200000)}2`;
    const start = performance.now();
    assert.equal(normalizeText(` ${spaces} `, 'number'), spaces);
    assert.equal(matchDate(dots, { y: 2026, m: 11, d: 8 }), null);
    assert.equal(performance.now() - start < 1000, true);
    assert.deepEqual(matchDate('1105\n.', { y: 2026, m: 11, d: 8 }, JA), { y: 2026, m: 11, d: 5 });
});

test('digits of every script are read, and lists ignore width differences', () => {
    assert.equal(normalizeText('۱۲۳', 'number'), '123');
    assert.equal(normalizeText('१२३', 'number'), '123');
    assert.equal(normalizeText('Ｓａｌｅｓ', 'list'), 'Ｓａｌｅｓ');
    const list = { kind: 'list', items: [{ value: 'Sales', text: 'Sales' }, { value: 'Ｂ２', text: 'Ｂ２' }] };
    assert.equal(checkInput(list, 'Ｓａｌｅｓ', messages).error, null);
    assert.equal(checkInput(list, 'b2', messages).error, null);
    assert.equal(checkInput(list, 'Support', messages).error, messages.list);
    const short = rule('list', { items: items(['A', 'Apple']), maxLength: 1 });
    assert.equal(error(short, 'Apple'), null);
});

test('edit formats keep quoted text and backslash-escaped characters as they are', () => {
    const date = { y: 2025, m: 11, d: 5 };
    assert.equal(formatDate(date, compileReading({ editDate: 'yyyy"年"M"月"d"日"' })), '2025年11月5日');
    assert.equal(formatDate(date, compileReading({ editDate: 'yyyy\\/MM\\/dd' })), '2025/11/05');
    assert.equal(formatDate(date, compileReading({ editDate: "yyyy'/'MM'/'dd" })), '2025/11/05');
    assert.equal(formatDate(date, compileReading({ ...LANGUAGES['ja-JP'], editDate: 'ggge"年"M"月"d"日"' })), '令和7年11月5日');
});

test('replayed keys find the line start and delete whole characters', () => {
    const lf = String.fromCharCode(10);
    assert.equal(lineStartOf(lf + 'abc', 0), 0);
    assert.equal(lineStartOf('ab' + lf + 'cd', 4), 3);
    assert.equal(lineStartOf('ab' + lf + 'cd', 2), 0);
    assert.equal(deleteSize('a𠮷', 3, true), 2);
    assert.equal(deleteSize('𠮷b', 0, false), 2);
    assert.equal(deleteSize('ab', 1, true), 1);
    assert.equal(deleteSize('ab', 0, false), 1);
});

test('the current list item is found whatever the width of the text', () => {
    const items = [{ text: 'Osaka', value: 'o' }, { text: 'tokyo', value: 't' }];
    assert.equal(currentItem(items, 'ｔｏｋｙｏ'), 1);
    assert.equal(currentItem(items, 'O'), 0);
    assert.equal(currentItem(items, 'nagoya'), -1);
    assert.equal(currentItem(items, ' tokyo '), 1);
});

test('suggestions match letter by letter in lower case and trim the same as the commit', () => {
    const greek = items(['x', 'οδοσ'], ['y', 'οδος']);
    assert.deepEqual(filterItems(greek, 'ΟΔΟΣ').map(x => x.text), ['οδοσ']);
    assert.equal(filterItems(items(['s', '営業部']), String.fromCharCode(0x85) + '営業').length, 1);
    assert.equal(currentItem([{ text: 'tokyo', value: 't' }], String.fromCharCode(0x85) + 'tokyo'), 0);
});

test('list items match letter by letter in lower case, the same as the commit', () => {
    const dotted = 'i' + String.fromCharCode(0x307);
    const capital = String.fromCharCode(0x130);
    assert.equal(currentItem([{ text: 'οδος', value: 'x' }], 'ΟΔΟΣ'), -1);
    assert.equal(currentItem([{ text: 'οδοσ', value: 'x' }], 'ΟΔΟΣ'), 0);
    assert.equal(currentItem([{ text: dotted, value: 'x' }], capital), -1);
    assert.equal(checkInput({ kind: 'list', items: items(['x', 'οδος']) }, 'ΟΔΟΣ', messages).error, messages.list);
    assert.equal(checkInput({ kind: 'list', items: items(['x', 'οδοσ']) }, 'ΟΔΟΣ', messages).error, null);
    assert.equal(checkInput({ kind: 'list', items: items(['x', dotted]) }, capital, messages).error, messages.list);
    assert.equal(filterItems(items(['x', 'οδος']), 'ΟΔΟΣ').length, 0);
    assert.equal(filterItems(items(['x', 'οδοσ']), 'ΟΔΟΣ').length, 1);
    assert.equal(filterItems(items(['x', dotted]), capital).length, 0);
});

test('cells follow rows and columns the app inserts or deletes', () => {
    assert.deepEqual(shiftCell({ r: 4, c: 1 }, { axis: 'row', index: 2, count: 1 }), { r: 5, c: 1 });
    assert.deepEqual(shiftCell({ r: 4, c: 1 }, { axis: 'row', index: 5, count: 2 }), { r: 4, c: 1 });
    assert.deepEqual(shiftCell({ r: 4, c: 1 }, { axis: 'row', index: 3, count: -1 }), { r: 3, c: 1 });
    assert.deepEqual(shiftCell({ r: 4, c: 1 }, { axis: 'row', index: 3, count: -2 }), { r: 3, c: 1, lost: true });
    assert.deepEqual(shiftCell({ r: 4, c: 3 }, { axis: 'col', index: 0, count: -2 }), { r: 4, c: 1 });
});

test('the column where a Tab run started follows the columns the app inserts or deletes', () => {
    const at = { row: 1, col: 3, anchor: { r: 1, c: 3 }, extent: { r: 1, c: 3 }, tabStartCol: 1 };
    assert.equal(shiftSelection(at, [{ axis: 'col', index: 0, count: 2 }], 5, 8).tabStartCol, 3);
    assert.equal(shiftSelection(at, [{ axis: 'col', index: 2, count: 1 }], 5, 8).tabStartCol, 1);
    assert.equal(shiftSelection(at, [{ axis: 'col', index: 1, count: -1 }], 5, 8).tabStartCol, -1);
    assert.equal(shiftSelection(at, [{ axis: 'row', index: 0, count: 1 }], 5, 8).tabStartCol, 1);
    assert.equal(shiftSelection({ ...at, tabStartCol: 6 }, [], 5, 4).tabStartCol, -1);
    assert.equal(shiftSelection({ ...at, tabStartCol: -1 }, [{ axis: 'col', index: 0, count: 2 }], 5, 8).tabStartCol, -1);
});

test('a caret moved to another line never stops inside a surrogate pair', () => {
    const value = 'ab\n\u{20BB7}\u{20BB7}\nc';
    assert.equal(lineCaret(value, 1, false), 3);
    assert.equal(lineCaret(value, 2, false), 5);
    assert.equal(lineCaret(value, 9, true), 3);
    assert.equal(lineCaret(value, 5, true), 2);
    assert.equal(lineCaret(value, 0, true), 0);
    assert.equal(lineCaret(value, 8, false), 9);
});

test('the active cell of a deleted last row or column stays inside the sheet', () => {
    assert.deepEqual(
        shiftSelection({ row: 4, col: 1, anchor: { r: 4, c: 1 }, extent: { r: 4, c: 2 } }, [{ axis: 'row', index: 4, count: -1 }], 4, 3),
        { row: 3, col: 1, anchor: { r: 3, c: 1 }, extent: { r: 3, c: 2 }, lost: true, tabStartCol: -1 });
    assert.deepEqual(
        shiftSelection({ row: 1, col: 2, anchor: { r: 1, c: 2 }, extent: { r: 1, c: 2 } }, [{ axis: 'col', index: 2, count: -1 }], 5, 2),
        { row: 1, col: 1, anchor: { r: 1, c: 1 }, extent: { r: 1, c: 1 }, lost: true, tabStartCol: -1 });
    assert.deepEqual(
        shiftSelection({ row: 2, col: 0, anchor: { r: 2, c: 0 }, extent: { r: 2, c: 0 } }, [{ axis: 'row', index: 0, count: 1 }], 4, 3),
        { row: 3, col: 0, anchor: { r: 3, c: 0 }, extent: { r: 3, c: 0 }, lost: false, tabStartCol: -1 });
    assert.deepEqual(
        shiftSelection({ row: 6, col: 4, anchor: { r: 6, c: 4 }, extent: { r: 6, c: 4 } }, [], 3, 2),
        { row: 2, col: 1, anchor: { r: 2, c: 1 }, extent: { r: 2, c: 1 }, lost: true, tabStartCol: -1 });
});
