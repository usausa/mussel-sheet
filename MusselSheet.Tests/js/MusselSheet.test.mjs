import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    normalizeText,
    prepareText,
    checkInput,
    convertCharacters,
    formatMask,
    maskDisplay,
    isAllowed,
    filterItems,
    parseDate,
    matchDate,
    parseNumber,
    matchWareki,
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
} from '../../MusselSheet/wwwroot/MusselSheet.js';

const JA = { decimal: '.', group: ',', order: 'ymd', editDate: 'yyyy/MM/dd', japanese: true };
const US = { decimal: '.', group: ',', order: 'mdy', editDate: 'M/d/yyyy', japanese: false };
const DE = { decimal: ',', group: '.', order: 'dmy', editDate: 'dd.MM.yyyy', japanese: false };

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
    fullwidth: '全角で入力してください',
    halfwidth: '半角で入力してください',
    hiragana: 'ひらがなで入力してください',
    katakana: '全角カタカナで入力してください',
    halfwidthKatakana: '半角カタカナで入力してください',
    alphanumeric: '半角英数字で入力してください',
    digits: '半角数字で入力してください',
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
        conversions: 0,
        suggest: false,
        ...extra,
    };
}

function items(...pairs) {
    return pairs.map(([value, text]) => ({ value, text: text ?? value }));
}

function error(r, text) {
    return checkInput(r, text, messages).error;
}

test('数値・日付・時刻では全角を半角にし、前後の空白を取る', () => {
    assert.equal(normalizeText('１，２３４', 'number'), '1,234');
    assert.equal(normalizeText('ー５', 'number'), '-5');
    assert.equal(normalizeText('　２０２６／１１／０１ ', 'date'), '2026/11/01');
    assert.equal(normalizeText('２０２６・１２・２４', 'date', JA), '2026/12/24');
    assert.equal(normalizeText('２０２６・１２・２４', 'date'), '2026・12・24');
    assert.equal(normalizeText('1・5', 'number'), '1・5');
    assert.equal(normalizeText('９：０５', 'time'), '9:05');
    assert.equal(normalizeText('　あ　', 'text'), '　あ　');
    assert.equal(normalizeText('  営業部 ', 'list'), '営業部');
});

test('数値は桁区切りを許し、数字のない文字を受け付けない', () => {
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

test('必須は空の入力をエラーにする', () => {
    assert.equal(error(rule('text', { required: true }), ''), messages.required);
    assert.equal(error(rule('number', { required: true }), '　'), messages.required);
    assert.equal(error(rule('text'), ''), null);
});

test('最小・最大はメッセージ用の文字で知らせる', () => {
    const range = rule('number', { minimum: '0', maximum: '100', minimumText: '0', maximumText: '100' });
    assert.equal(error(range, '100'), null);
    assert.equal(error(range, '101'), '0 から 100 の範囲で入力してください');
    assert.equal(error(rule('number', { minimum: '10', minimumText: '10' }), '9'), '10 以上で入力してください');
    assert.equal(error(rule('number', { maximum: '1000', maximumText: '1,000' }), '1,001'), '1,000 以下で入力してください');
});

test('日付は存在する日だけを受け付け、範囲を比べる', () => {
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

test('時刻は H:mm と H:mm:ss を受け付ける', () => {
    const time = rule('time', { maximum: '18:00:00', maximumText: '18:00' });
    assert.equal(error(time, '9:05'), null);
    assert.equal(error(time, '09:05:30'), null);
    assert.equal(error(time, '24:00'), messages.time);
    assert.equal(error(time, '18:01'), '18:00 以下で入力してください');
});

test('チェックと選択リスト', () => {
    assert.equal(error(rule('check'), 'TRUE'), null);
    assert.equal(error(rule('check'), 'yes'), messages.check);
    const list = rule('list', { items: items(['S', '営業部'], ['G', '総務部']) });
    assert.equal(error(list, '営業部'), null);
    assert.equal(error(list, 's'), null);
    assert.equal(error(list, '開発部'), messages.list);
    assert.equal(error(rule('list'), '何でも'), null);
    assert.equal(error(rule('autocomplete', { items: items(['東京']) }), '大阪'), null);
});

test('文字の自動変換(全角英数字→半角、半角カナ→全角、ひらがな⇔カタカナ)', () => {
    assert.equal(convertCharacters('ＡＢＣ１２３　＃', 1), 'ABC123 #');
    assert.equal(convertCharacters('ｶﾞｷﾞｸﾞ ﾊﾟﾋﾟ ｳﾞ ｱｲｳ ﾞ', 2), 'ガギグ パピ ヴ アイウ ゛');
    assert.equal(convertCharacters('やまだ たろう ゝ', 4), 'ヤマダ タロウ ヽ');
    assert.equal(convertCharacters('ヤマダ ヴ', 8), 'やまだ ゔ');
    assert.equal(convertCharacters('ｻﾄｳ', 2 | 8), 'さとう');
    assert.equal(convertCharacters('そのまま', 0), 'そのまま');
});

test('文字種の制限', () => {
    assert.equal(isAllowed('ヤマダ　タロウ・ー', 'katakana'), true);
    assert.equal(isAllowed('ヤマダ太郎', 'katakana'), false);
    assert.equal(isAllowed('やまだ ー', 'hiragana'), true);
    assert.equal(isAllowed('ﾔﾏﾀﾞ', 'halfwidthKatakana'), true);
    assert.equal(isAllowed('ＡＢＣあ', 'fullwidth'), true);
    assert.equal(isAllowed('ABCあ', 'fullwidth'), false);
    assert.equal(isAllowed('ABC ﾔ', 'halfwidth'), true);
    assert.equal(isAllowed('A1b2', 'alphanumeric'), true);
    assert.equal(isAllowed('A1-2', 'alphanumeric'), false);
    assert.equal(isAllowed('0123', 'digits'), true);
    assert.equal(isAllowed('12a', 'digits'), false);
    assert.equal(isAllowed('何でも', null), true);
});

test('文字種の制限は変換の後で見て、文字で入力する種類だけに効く', () => {
    const kana = rule('text', { restriction: 'katakana', conversions: 2 | 4 });
    assert.equal(error(kana, 'やまだ ﾀﾛｳ'), null);
    assert.equal(checkInput(kana, 'やまだ ﾀﾛｳ', messages).text, 'ヤマダ タロウ');
    assert.equal(error(kana, '山田'), messages.katakana);
    assert.equal(error(rule('text', { restriction: 'digits' }), '12a'), messages.digits);
    assert.equal(error(rule('number', { restriction: 'katakana' }), '12'), null);
});

test('マスク: 区切りは省いても別の区切りで打ってもよく、確定の前にマスクの形に整える', () => {
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
    assert.equal(error(zip, '１２３ー４５６７'), null);
    assert.equal(error(zip, '12345'), '999-9999 の形で入力してください');
    assert.equal(error(zip, ''), null);
});

test('候補の絞り込みは前方一致を先に、部分一致を後に並べる', () => {
    const list = items(['G', '総務部'], ['S', '営業部'], ['D', '開発部'], ['E', '営業推進部']);
    assert.deepEqual(filterItems(list, '営業').map(x => x.text), ['営業部', '営業推進部']);
    assert.deepEqual(filterItems(list, '部').map(x => x.text), ['総務部', '営業部', '開発部', '営業推進部']);
    assert.deepEqual(filterItems(list, 'd').map(x => x.text), ['開発部']);
    assert.deepEqual(filterItems(list, '推進').map(x => x.text), ['営業推進部']);
    assert.equal(filterItems(list, '  ').length, 4);
});

test('カレンダーの日付の計算', () => {
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

test('文字数はサロゲートペアを1文字、バイト数は全角を2バイトとして数える', () => {
    assert.equal(error(rule('text', { maxLength: 3 }), '𠮷野家'), null);
    assert.equal(error(rule('text', { maxLength: 2 }), '𠮷野家'), '2 文字以内で入力してください');
    assert.equal(countBytes('ｱｲｳ'), 3);
    assert.equal(countBytes('あい'), 4);
    assert.equal(countBytes('A1あ'), 4);
    assert.equal(error(rule('text', { maxByteLength: 4 }), 'あいう'), '4 バイト以内で入力してください(全角は2バイト)');
});

test('正規表現は文字列全体に合うかを見る。使えない正規表現は判定しない', () => {
    const zip = rule('text', { pattern: '[0-9]{3}-[0-9]{4}' });
    assert.equal(error(zip, '123-4567'), null);
    assert.equal(error(zip, 'x123-4567'), messages.pattern);
    assert.equal(error(rule('text', { pattern: '(' }), 'abc'), null);
});

test('セル設定のエラーメッセージがあれば、そちらを使う', () => {
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

test('右方向で行の最後から次の行へ進むと Z 型になる(入力禁止セルと小計の行を飛ばす)', () => {
    assert.deepEqual(walk({ r: 0, c: 1 }, 'right', true, true), ['0,2', '1,1', '1,2', '3,1', '3,2']);
    assert.deepEqual(walk({ r: 3, c: 2 }, 'left', true, true), ['3,1', '1,2', '1,1', '0,2', '0,1']);
});

test('下方向は同じ列の次の入力セルへ進み、列の最後で止まる', () => {
    assert.deepEqual(walk({ r: 0, c: 1 }, 'down', true, false), ['1,1', '3,1']);
});

test('飛ばさないときは入力禁止セルにも止まり、表の端で止まる', () => {
    assert.deepEqual(findNext(sample, 1, 1, 'down', false, false), { r: 2, c: 1 });
    assert.equal(findNext(sample, 0, 3, 'right', false, false), null);
    assert.equal(findNext(sample, 0, 0, 'up', false, false), null);
});

test('最初の入力セル', () => {
    assert.deepEqual(firstInputCell(sample), { r: 0, c: 1 });
    assert.deepEqual(firstInputCell({ rows: 2, cols: 2, isDisabled: () => true }), { r: 0, c: 0 });
});

test('セルの位置は見出しと、行ごとの高さ・列ごとの幅から求める', () => {
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

test('貼り付ける文字は表の最初のセルだけ(Excel の末尾の改行と引用符にも対応する)', () => {
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

test('クリップボードの表は Excel の TSV の書き方で分ける(C# の ClipboardFormat と同じ)', () => {
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

test('TSV と HTML の表を作る(C# の ClipboardFormat と同じ)', () => {
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

test('範囲の中の Enter・Tab は端で次の列・行へ進み、最後から最初へ戻る(入力禁止セルは飛ばす)', () => {
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

test('位置から行・列の番号を求め(範囲の外は端)、2つの角から範囲を作る', () => {
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

test('改行は、複数行のテキストでは LF にそろえ、それ以外のセルでは取り除く(C# の NormalizeLines と同じ)', () => {
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

test('右クリックメニューの種類は、行全体・列全体を選んでいれば行・列のメニュー、それ以外はセルのメニュー', () => {
    const size = { rows: 10, cols: 4 };
    assert.equal(menuKind({ top: 2, left: 1, bottom: 2, right: 1 }, size), 'cell');
    assert.equal(menuKind({ top: 2, left: 0, bottom: 3, right: 3 }, size), 'row');
    assert.equal(menuKind({ top: 0, left: 1, bottom: 9, right: 2 }, size), 'column');
    assert.equal(menuKind({ top: 0, left: 0, bottom: 9, right: 3 }, size), 'cell');
    assert.equal(menuKind({ top: 1, left: 1, bottom: 3, right: 2 }, size), 'cell');
});

test('右クリックメニューは、クリップボードの項目と、許可した行・列の挿入・削除を出す(選んだ数を入れる)', () => {
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

test('入力できるセルがない範囲では切り取り・貼り付けを、全部の行・列を選んでいるときは削除を使えなくする', () => {
    const size = { rows: 3, cols: 2 };
    const entries = menuEntries('cell', { top: 0, left: 0, bottom: 2, right: 0 }, size, allowAll, false, menuMessages);
    const disabled = entries.filter(x => x?.disabled).map(x => x.action);
    assert.deepEqual(disabled, ['cut', 'paste', 'deleteRows']);
});

test('メニューのキーでの移動は、区切りと使えない項目を飛ばし、端では反対の端へ回る', () => {
    const entries = [{ action: 'cut', disabled: true }, { action: 'copy' }, { action: 'paste', disabled: true }, null, { action: 'insertRowsAbove' }];
    assert.equal(nextMenuIndex(entries, -1, 1), 1);
    assert.equal(nextMenuIndex(entries, -1, -1), 4);
    assert.equal(nextMenuIndex(entries, 1, 1), 4);
    assert.equal(nextMenuIndex(entries, 4, 1), 1);
    assert.equal(nextMenuIndex(entries, 1, -1), 4);
    assert.equal(nextMenuIndex([{ action: 'cut', disabled: true }, null], -1, 1), -1);
});

test('固定した行・列があるとき、セルのある部分(ペイン)を決める', () => {
    assert.equal(paneOf(0, 0, 1, 1), 'tl');
    assert.equal(paneOf(0, 5, 1, 1), 't');
    assert.equal(paneOf(3, 0, 1, 1), 'l');
    assert.equal(paneOf(3, 5, 1, 1), 'main');
    assert.equal(paneOf(0, 0, 0, 0), 'main');
});

test('選択範囲は固定した行・列の境界で分け、外側の辺だけに枠の線を引く', () => {
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

test('固定した行・列の上の位置は、貼り付いている位置から番号を求める', () => {
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

test('結合した範囲は、どのセルからも引け、左上以外のセルは入力しない。位置は範囲全体', () => {
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

test('選択範囲は、一部だけかかる結合した範囲を含むまで広げる', () => {
    const map = mergedSheet();
    assert.deepEqual(map.expandRange({ top: 1, left: 2, bottom: 2, right: 2 }), { top: 1, left: 0, bottom: 2, right: 3 });
    assert.deepEqual(map.expandRange({ top: 6, left: 0, bottom: 6, right: 1 }), { top: 6, left: 0, bottom: 6, right: 1 });
    assert.deepEqual(map.expandRange({ top: 5, left: 0, bottom: 5, right: 0 }), { top: 3, left: 0, bottom: 5, right: 0 });
    assert.deepEqual(map.expandRange({ top: 5, left: 0, bottom: 5, right: 1 }), { top: 3, left: 0, bottom: 5, right: 3 });
});

test('結合した範囲は1つのセルとして動く(入るときは左上のセル、出るときは範囲の端から)', () => {
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

test('範囲の中の Enter・Tab は、結合した範囲の左上以外のセルに止まらない', () => {
    const map = mergedSheet();
    const range = { top: 1, left: 1, bottom: 3, right: 3 };
    assert.deepEqual(nextInRange(map, range, 1, 1, 'row', false, false), { r: 2, c: 3 });
    assert.deepEqual(nextInRange(map, range, 2, 3, 'row', false, false), { r: 3, c: 1 });
});

test('日付の簡易入力は、年を省いた月/日を今年にし、2桁の年・数字だけ・漢字の形も受け付ける', () => {
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

test('移動の範囲の中は、範囲を選んでいるときと同じく巡回する', () => {
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

test('名前のないセルからは、名前のあるセルを飛ばしてこれまでどおり動く', () => {
    const map = groupedSheet();
    const free = (r, c) => map.groupAt(r, c) === null;
    assert.deepEqual(findNext(map, 1, 2, 'right', true, true, free), { r: 2, c: 0 });
    assert.deepEqual(findNext(map, 1, 2, 'right', true, true), { r: 1, c: 3 });
    assert.equal(findNext(map, 1, 1, 'up', true, false, free), null);
    assert.equal(findNext(map, 3, 2, 'right', true, true, free), null);
});

test('カレンダーの週の始まりと年月の書き方は、カルチャに合わせる', () => {
    assert.deepEqual(monthGrid(2026, 11, 1)[0], { y: 2026, m: 10, d: 26 });
    assert.deepEqual(monthGrid(2026, 11)[0], { y: 2026, m: 11, d: 1 });
    const names = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
    assert.equal(formatYearMonth('yyyy年M月', 2026, 11, names), '2026年11月');
    assert.equal(formatYearMonth('MMMM yyyy', 2026, 11, names), 'November 2026');
    assert.equal(formatYearMonth("yyyy'/'MM", 2026, 3, names), '2026/03');
});

test('数値の小数点・桁区切りと日付の並びは、シートのカルチャに合わせる', () => {
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
    assert.equal(parseNumber('1 234,5', { decimal: ',', group: '\u202f', order: 'dmy', editDate: 'dd/MM/yyyy', japanese: false }), 1234.5);
    assert.equal(checkInput(rule('number'), '1.234,5', messages, DE).error, null);
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }, US), '11/5/2025');
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }, DE), '05.11.2025');
    assert.equal(formatDate({ y: 2025, m: 11, d: 5 }), '2025/11/05');
});

test('キーは Ctrl・Alt・Shift の順に書き、1 文字のキーは大文字、空白は Space にそろえる', () => {
    assert.equal(comboOf({ key: 'd', ctrl: true, alt: false, shift: false }), 'Ctrl+D');
    assert.equal(comboOf({ key: 'Enter', ctrl: true, alt: false, shift: true }), 'Ctrl+Shift+Enter');
    assert.equal(comboOf({ key: 'F4', ctrl: false, alt: false, shift: false }), 'F4');
    assert.equal(comboOf({ key: ' ', ctrl: false, alt: true, shift: false }), 'Alt+Space');
});

test('右クリックメニューは、隠した組み込みの項目を出さず、アプリの項目を最後に足す', () => {
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

const ERAS = [
    { name: '明治', initial: 'M', start: '1868-09-08' },
    { name: '大正', initial: 'T', start: '1912-07-30' },
    { name: '昭和', initial: 'S', start: '1926-12-25' },
    { name: '平成', initial: 'H', start: '1989-01-08' },
    { name: '令和', initial: 'R', start: '2019-05-01' },
];

test('和暦は元号の漢字と英字の頭文字で読み、元号の終わりを超えた年も受け付ける', () => {
    const cases = [
        ['令和7年11月5日', 2025, 11, 5], ['令和元年5月1日', 2019, 5, 1], ['令和7/11/5', 2025, 11, 5], ['令和7.11.5', 2025, 11, 5],
        ['R7.11.5', 2025, 11, 5], ['r7/11/5', 2025, 11, 5], ['H31.4.30', 2019, 4, 30], ['H31.5.1', 2019, 5, 1],
        ['H32.1.1', 2020, 1, 1], ['平成32年1月1日', 2020, 1, 1], ['S64.1.7', 1989, 1, 7], ['M45.7.29', 1912, 7, 29],
    ];
    for (const [text, y, m, d] of cases) {
        assert.deepEqual(matchWareki(text, ERAS), { y, m, d }, text);
    }
    for (const text of ['令和1年4月30日', 'R0.1.1', 'X7.1.1', 'R071105', '令和7年11月5', 'R7.13.1']) {
        assert.equal(matchWareki(text, ERAS), null, text);
    }

    const base = { y: 2026, m: 11, d: 2 };
    assert.deepEqual(matchDate('R7.11.5', base, { ...JA, eras: ERAS }), { y: 2025, m: 11, d: 5 });
    assert.equal(matchDate('R7.11.5', base, { ...US, eras: ERAS }), null);
    assert.deepEqual(parseDate('Ｒ７．１１．５', { ...JA, eras: ERAS }), { y: 2025, m: 11, d: 5 });
});

test('フィルハンドルをドラッグした先までの範囲', () => {
    const source = { top: 2, left: 1, bottom: 3, right: 2 };
    assert.deepEqual(fillTarget(source, { r: 6, c: 2 }), { top: 2, left: 1, bottom: 6, right: 2 });
    assert.deepEqual(fillTarget(source, { r: 0, c: 1 }), { top: 0, left: 1, bottom: 3, right: 2 });
    assert.deepEqual(fillTarget(source, { r: 4, c: 6 }), { top: 2, left: 1, bottom: 3, right: 6 });
    assert.deepEqual(fillTarget(source, { r: 3, c: 0 }), { top: 2, left: 0, bottom: 3, right: 2 });
    assert.deepEqual(fillTarget(source, { r: 5, c: 4 }), { top: 2, left: 1, bottom: 5, right: 2 });
    assert.equal(fillTarget(source, { r: 3, c: 1 }), null);
});

test('補正の関数があるセルは型だけを確かめる', () => {
    const limited = rule('number', { maximum: '1000', required: true, corrects: true });
    assert.equal(error(limited, '2000'), null);
    assert.equal(error(limited, ''), null);
    assert.equal(error(limited, 'abc'), messages.number);
    assert.equal(error(rule('number', { maximum: '1000' }), '2000') !== null, true);
    assert.equal(error(rule('list', { items: items(['A']), corrects: true }), 'a-1'), null);
});
