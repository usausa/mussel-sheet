// Key, IME and focus handling lives here; C# decides value conversion, validation, applying and undo.
// Calls into C# are per intent (commit, clear, toggle, undo, active-cell move), never per keystroke.

const READY = 'ready';
const INPUT = 'input';
const EDIT = 'edit';
const COMMITTING = 'committing';
const CUSTOM = 'custom';

const DEFAULT_OPTIONS = {
    enterDirection: 'down',   // down | right | none
    readyEnter: 'move',       // move | edit
    tabReturn: true,
    rowEnd: 'next',           // next | stop | leave
    skipReadOnly: 'enterTab', // none | enterTab | all
    lastRowEnter: 'stop',  // stop | add
    commitMode: 'optimistic', // optimistic | wait
    autosize: false,
    customEditor: false,
    allowInsertRows: false,
    allowDeleteRows: false,
    allowInsertColumns: false,
    allowDeleteColumns: false,
    allowFill: true,
    frozenRows: 0,
    frozenColumns: 0,
};

// Safari can deliver the Enter that confirms a composition with isComposing=false,
// so an Enter right after compositionend is ignored.
const COMPOSITION_ENTER_GUARD_MS = 30;

const ACTIVE_NOTIFY_DELAY_MS = 50;

const SUGGEST_DELAY_MS = 250;

const LIST_PAGE = 8;

// Nudges the IME toward direct input; on Windows, inputmode becomes the IME input scope
const HALFWIDTH_INPUT_MODE = 'url';

const QUEUED_KEYS = new Set(['Enter', 'Tab', 'Escape', 'Backspace', 'Delete', 'F2', 'Home', 'End', 'PageUp', 'PageDown', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);

const FOCUSABLE = 'input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex="-1"])';

const SUPPORTS_POPOVER = typeof HTMLElement !== 'undefined' && typeof HTMLElement.prototype.showPopover === 'function';

// Command names must match C# SheetCommands
const COMMAND_KEYS = {
    edit: { key: 'F2' },
    clear: { key: 'Delete' },
    undo: { key: 'z', ctrl: true },
    redo: { key: 'y', ctrl: true },
    selectAll: { key: 'a', ctrl: true },
    fillDown: { key: 'd', ctrl: true },
    fillRight: { key: 'r', ctrl: true },
    today: { key: ';', ctrl: true },
    fillSelection: { key: 'Enter', ctrl: true },
    menu: { key: 'F10', shift: true },
    openList: { key: 'ArrowDown', alt: true },
    moveNext: { key: 'Enter' },
    movePrevious: { key: 'Enter', shift: true },
    tabNext: { key: 'Tab' },
    tabPrevious: { key: 'Tab', shift: true },
    cancel: { key: 'Escape' },
    newLine: { key: 'Enter', alt: true },
};

const EXTEND_KEYS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown']);

const AUTO_SCROLL_MS = 50;

// Makes Excel paste the cell as text so leading zeros survive; must match C# ClipboardFormat
const TEXT_CELL = '<td class="istext" style=\'mso-number-format:"\\@"\'>';

// Must match C# ClipboardFormat. Uses <style> classes like Excel's own copy;
// mso-data-placement makes <br> a line break inside the same cell.
const EXCEL_STYLE = '<style>.istext{mso-number-format:"\\@";}br{mso-data-placement:same-cell;}</style>';

const DEFAULT_CALENDAR = {
    dayNames: ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    firstDay: 0,
    yearMonth: 'MMMM yyyy',
    monthNames: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
};

let noteSequence = 0;

const SHORTCUT_PREFIX = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform ?? '') ? '⌘' : 'Ctrl+';

export function attach(root, dotnet, options, map) {
    return new SheetController(root, dotnet, options, map);
}

// ---- Input normalization and quick validation (same rules as C# CellInput; change both together) ----

const HALFWIDTH_KINDS = new Set(['number', 'date', 'time', 'check']);
const STRING_KINDS = new Set(['text', 'mask', 'autocomplete']);
const YEAR_FIRST_DATE_TEXT = /^([0-9]{4})[/.-]([0-9]{1,2})[/.-]([0-9]{1,2})$/;
const SHORT_YEAR_FIRST_DATE_TEXT = /^([0-9]{2})[/.-]([0-9]{1,2})[/.-]([0-9]{1,2})$/;
const YEAR_LAST_DATE_TEXT = /^([0-9]{1,2})[/.-]([0-9]{1,2})[/.-]([0-9]{2}|[0-9]{4})$/;
const YEARLESS_DATE_TEXT = /^([0-9]{1,2})[/.-]([0-9]{1,2})$/;
const KANJI_DATE_TEXT = /^(?:([0-9]{4}|[0-9]{2})年)?([0-9]{1,2})月([0-9]{1,2})日$/;
const DIGITS_DATE_TEXT = /^(?:[0-9]{4}|[0-9]{6}|[0-9]{8})$/;
const TIME_TEXT = /^([0-9]{1,2}):([0-9]{2})(?::([0-9]{2}))?$/;
const BOUND_DATE = /^[0-9]{4}-[0-9]{2}-[0-9]{2}$/;
const BOUND_TIME = /^[0-9]{2}:[0-9]{2}:[0-9]{2}$/;

const FULLWIDTH_KANA = '。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン゛゜';
const VOICED_KANA = 'カキクケコサシスセソタチツテトハヒフヘホ';
const SEMI_VOICED_KANA = 'ハヒフヘホ';

// Must match the values of C# CharacterConversions
const CONVERT_FULLWIDTH_ASCII = 1;
const CONVERT_HALFWIDTH_KATAKANA = 2;
const CONVERT_HIRAGANA_TO_KATAKANA = 4;
const CONVERT_KATAKANA_TO_HIRAGANA = 8;

const DEFAULT_READING = { decimal: '.', group: ',', order: 'ymd', editDate: 'yyyy/MM/dd', japanese: false };

// With the Japanese reading, '・' in a date becomes '/':
// a Japanese IME produces it when '/' is typed.
export function normalizeText(text, kind, reading = DEFAULT_READING) {
    if (kind === 'list') {
        return text.trim();
    }

    if (kind === 'date') {
        const out = normalizeWidth(text);
        return reading.japanese ? out.replace(/・/g, '/') : out;
    }

    return HALFWIDTH_KINDS.has(kind) ? normalizeWidth(text) : text;
}

export function prepareText(rule, text, reading = DEFAULT_READING) {
    const lines = normalizeLines(text, rule.multiline);
    const mask = rule.mask;
    let out = mask ? normalizeWidth(lines) : normalizeText(lines, rule.kind, reading);
    out = convertCharacters(out, rule.conversions ?? 0);
    if (mask) {
        const formatted = formatMask(out, mask);
        if (formatted !== null) {
            return formatted;
        }
    }

    return out;
}

export function normalizeLines(text, multiline) {
    return multiline ? text.replace(/\r\n|\r/g, '\n') : text.replace(/\r\n|\n|\r/g, '');
}

// A Japanese IME types the hyphen key as the long-vowel mark, so it and other dash lookalikes become '-'
function normalizeWidth(text) {
    let out = '';
    for (const ch of text) {
        const code = ch.charCodeAt(0);
        if (code >= 0xFF01 && code <= 0xFF5E) {
            out += String.fromCharCode(code - 0xFEE0);
        } else if (ch === '　') {
            out += ' ';
        } else if (ch === 'ー' || ch === '−' || ch === '‐' || ch === '―') {
            out += '-';
        } else {
            out += ch;
        }
    }

    return out.trim();
}

export function convertCharacters(text, flags) {
    if (flags & CONVERT_FULLWIDTH_ASCII) {
        text = toHalfwidthAscii(text);
    }

    if (flags & CONVERT_HALFWIDTH_KATAKANA) {
        text = toFullwidthKatakana(text);
    }

    if (flags & CONVERT_HIRAGANA_TO_KATAKANA) {
        text = shiftKana(text, 0x3041, 0x3096, 0x309D, 0x60);
    }

    if (flags & CONVERT_KATAKANA_TO_HIRAGANA) {
        text = shiftKana(text, 0x30A1, 0x30F6, 0x30FD, -0x60);
    }

    return text;
}

function toHalfwidthAscii(text) {
    let out = '';
    for (let i = 0; i < text.length; i++) {
        const code = text.charCodeAt(i);
        if (code >= 0xFF01 && code <= 0xFF5E) {
            out += String.fromCharCode(code - 0xFEE0);
        } else {
            out += code === 0x3000 ? ' ' : text[i];
        }
    }
    return out;
}

function toFullwidthKatakana(text) {
    let out = '';
    for (let i = 0; i < text.length; i++) {
        const code = text.charCodeAt(i);
        if (code < 0xFF61 || code > 0xFF9F) {
            out += text[i];
            continue;
        }

        const kana = FULLWIDTH_KANA[code - 0xFF61];
        const next = text.charCodeAt(i + 1);
        const voiced = next === 0xFF9E ? voice(kana) : null;
        if (voiced) {
            out += voiced;
            i++;
        } else if (next === 0xFF9F && SEMI_VOICED_KANA.includes(kana)) {
            out += String.fromCharCode(kana.charCodeAt(0) + 2);
            i++;
        } else {
            out += kana;
        }
    }
    return out;
}

function voice(kana) {
    switch (kana) {
        case 'ウ':
            return 'ヴ';
        case 'ワ':
            return 'ヷ';
        case 'ヲ':
            return 'ヺ';
        default:
            return VOICED_KANA.includes(kana) ? String.fromCharCode(kana.charCodeAt(0) + 1) : null;
    }
}

function shiftKana(text, first, last, iteration, shift) {
    let out = '';
    for (let i = 0; i < text.length; i++) {
        const code = text.charCodeAt(i);
        const inRange = (code >= first && code <= last) || code === iteration || code === iteration + 1;
        out += inRange ? String.fromCharCode(code + shift) : text[i];
    }
    return out;
}

export function formatMask(text, mask) {
    let out = '';
    let i = 0;
    for (let m = 0; m < mask.length; m++) {
        let token = mask[m];
        const escaped = token === '\\' && m + 1 < mask.length;
        if (escaped) {
            token = mask[++m];
        }

        if (!escaped && (token === '9' || token === 'A' || token === 'X')) {
            if (i >= text.length || !fitsMask(token, text[i])) {
                return null;
            }
            out += text[i++];
            continue;
        }

        if (i < text.length && (text[i] === token || (!isAsciiAlnum(token) && isSeparator(text[i])))) {
            i++;
        }
        out += token;
    }

    return i === text.length ? out : null;
}

export function maskDisplay(mask) {
    return mask.replace(/\\(.)/g, '$1');
}

function fitsMask(token, ch) {
    switch (token) {
        case '9':
            return /^[0-9]$/.test(ch);
        case 'A':
            return /^[A-Za-z]$/.test(ch);
        default:
            return isAsciiAlnum(ch);
    }
}

function isAsciiAlnum(ch) {
    return /^[0-9A-Za-z]$/.test(ch);
}

function isSeparator(ch) {
    return ch.charCodeAt(0) <= 0x7F && !isAsciiAlnum(ch);
}

export function isAllowed(text, restriction) {
    if (!restriction) {
        return true;
    }

    for (const ch of text) {
        if (!allows(ch.codePointAt(0), restriction)) {
            return false;
        }
    }
    return true;
}

function isHalfwidthCode(code) {
    return code <= 0x7F || (code >= 0xFF61 && code <= 0xFF9F);
}

function allows(code, restriction) {
    switch (restriction) {
        case 'fullwidth':
            return !isHalfwidthCode(code);
        case 'halfwidth':
            return isHalfwidthCode(code);
        case 'hiragana':
            return (code >= 0x3041 && code <= 0x3096) || code === 0x309D || code === 0x309E || code === 0x30FC || code === 0x20 || code === 0x3000;
        case 'katakana':
            return (code >= 0x30A1 && code <= 0x30FE) || code === 0x20 || code === 0x3000;
        case 'halfwidthKatakana':
            return (code >= 0xFF65 && code <= 0xFF9F) || code === 0x20;
        case 'alphanumeric':
            return (code >= 0x30 && code <= 0x39) || (code >= 0x41 && code <= 0x5A) || (code >= 0x61 && code <= 0x7A);
        case 'digits':
            return code >= 0x30 && code <= 0x39;
        default:
            return true;
    }
}

export function checkInput(rule, text, messages, reading = DEFAULT_READING) {
    const normalized = prepareText(rule, text, reading);
    const error = findError(rule, normalized, messages, reading);
    return error === null
        ? { ok: true, text: normalized, error: null }
        : { ok: false, text: normalized, error: rule.message ?? error };
}

// A cell with a correction function (rule.corrects) is only type-checked here;
// the other rules run in C# against the corrected value.
function findError(rule, text, messages, reading) {
    if (text === '') {
        return rule.required && !rule.corrects ? messages.required : null;
    }

    let value = text;
    switch (rule.kind) {
        case 'number':
            value = parseNumber(text, reading);
            if (value === null) {
                return messages.number;
            }
            break;
        case 'date': {
            const date = matchDate(text, today(), reading);
            if (!date) {
                return messages.date;
            }
            value = dateKey(date);
            break;
        }
        case 'time': {
            const m = TIME_TEXT.exec(text);
            const second = m?.[3] ?? '0';
            if (!m || Number(m[1]) > 23 || Number(m[2]) > 59 || Number(second) > 59) {
                return messages.time;
            }
            value = `${pad2(m[1])}:${pad2(m[2])}:${pad2(second)}`;
            break;
        }
        case 'check':
            return /^(true|false)$/i.test(text) ? null : messages.check;
        case 'list':
            if (!rule.corrects && rule.items?.length && !rule.items.some(x => sameText(x.value, text) || sameText(x.text, text))) {
                return messages.list;
            }
            break;
        default:
            break;
    }

    if (rule.corrects) {
        return null;
    }

    return checkRange(rule, value, messages)
        ?? checkLength(rule, text, messages)
        ?? checkCharacters(rule, text, messages)
        ?? checkPattern(rule, text, messages);
}

function checkRange(rule, value, messages) {
    if (rule.minimum == null && rule.maximum == null) {
        return null;
    }

    const below = rule.minimum != null && compareBound(rule.kind, value, rule.minimum) < 0;
    const above = rule.maximum != null && compareBound(rule.kind, value, rule.maximum) > 0;
    if (!below && !above) {
        return null;
    }

    if (rule.minimumText != null && rule.maximumText != null) {
        return fill(messages.between, rule.minimumText, rule.maximumText);
    }

    return rule.minimumText != null ? fill(messages.atLeast, rule.minimumText) : fill(messages.atMost, rule.maximumText ?? '');
}

function compareBound(kind, value, bound) {
    switch (kind) {
        case 'number': {
            const limit = Number(bound);
            return Number.isNaN(limit) ? 0 : Math.sign(value - limit);
        }
        case 'date':
            return BOUND_DATE.test(bound) ? value.localeCompare(bound) : 0;
        case 'time':
            return BOUND_TIME.test(bound) ? value.localeCompare(bound) : 0;
        default:
            return 0;
    }
}

function checkLength(rule, text, messages) {
    if (HALFWIDTH_KINDS.has(rule.kind)) {
        return null;
    }

    if (rule.maxLength != null && [...text].length > rule.maxLength) {
        return fill(messages.maxLength, String(rule.maxLength));
    }

    if (rule.maxByteLength != null && countBytes(text) > rule.maxByteLength) {
        return fill(messages.maxByteLength, String(rule.maxByteLength));
    }

    return null;
}

function checkCharacters(rule, text, messages) {
    if (!STRING_KINDS.has(rule.kind)) {
        return null;
    }

    if (!isAllowed(text, rule.restriction)) {
        return messages[rule.restriction];
    }

    return rule.mask && formatMask(text, rule.mask) === null ? fill(messages.mask, maskDisplay(rule.mask)) : null;
}

// Matches the whole text; a pattern that JS cannot compile is left to C#
function checkPattern(rule, text, messages) {
    if (!rule.pattern) {
        return null;
    }

    let regex;
    try {
        regex = new RegExp(`^(?:${rule.pattern})$`);
    } catch {
        return null;
    }

    return regex.test(text) ? null : messages.pattern;
}

export function countBytes(text) {
    let count = 0;
    for (const ch of text) {
        count += isHalfwidthCode(ch.codePointAt(0)) ? 1 : 2;
    }
    return count;
}

function isValidDate(year, month, day) {
    if (year < 1 || month < 1 || month > 12 || day < 1) {
        return false;
    }

    return day <= daysInMonth(year, month);
}

function daysInMonth(year, month) {
    const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
    return [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1];
}

function pad2(text) {
    return String(text).padStart(2, '0');
}

function fill(template, first, second) {
    const text = template.replace('{0}', first);
    return second === undefined ? text : text.replace('{1}', second);
}

function sameText(a, b) {
    return a.toLowerCase() === b.toLowerCase();
}

// ---- Suggestions and calendar ----

export function filterItems(items, text) {
    const key = text.trim().toLowerCase();
    if (!key) {
        return items.slice();
    }

    const head = [];
    const rest = [];
    for (const item of items) {
        const label = item.text.toLowerCase();
        const value = item.value.toLowerCase();
        if (label.startsWith(key) || value.startsWith(key)) {
            head.push(item);
        } else if (label.includes(key) || value.includes(key)) {
            rest.push(item);
        }
    }
    return head.concat(rest);
}

// Same rules as C# CellInput.TryParseNumber.
// When the group separator is whitespace (French, etc.), a plain space is accepted too.
export function parseNumber(text, reading = DEFAULT_READING) {
    const pattern = numberPattern(reading);
    if (!/[0-9]/.test(text) || !pattern.full.test(text)) {
        return null;
    }

    return Number(text.replace(pattern.group, '').replace(reading.decimal, '.'));
}

const numberPatterns = new Map();

function numberPattern(reading) {
    const key = `${reading.decimal}|${reading.group}`;
    let entry = numberPatterns.get(key);
    if (!entry) {
        const group = /^\s/.test(reading.group) ? '[ \u00a0\u202f]' : escapeRegExp(reading.group);
        entry = {
            full: new RegExp(`^[+-]?(?:[0-9](?:[0-9]|${group})*)?(?:${escapeRegExp(reading.decimal)}[0-9]+)?$`),
            group: reading.group ? new RegExp(group, 'g') : /(?!)/g,
        };
        numberPatterns.set(key, entry);
    }
    return entry;
}

function escapeRegExp(text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export function parseDate(text, reading = DEFAULT_READING) {
    return matchDate(normalizeText(text, 'date', reading), today(), reading);
}

// Same rules as C# CellInput.TryParseDate; a leading 4-digit year is read as year-first in every culture
export function matchDate(text, base, reading = DEFAULT_READING) {
    const wareki = reading.japanese ? matchWareki(text, reading.eras) : null;
    if (wareki) {
        return wareki;
    }

    const parts = splitDate(text, reading);
    if (!parts) {
        return null;
    }

    const [yearText, monthText, dayText] = parts;
    let y = yearText === null ? base.y : Number(yearText);
    if (yearText?.length === 2) {
        y = fullYear(y);
    }

    const date = { y, m: Number(monthText), d: Number(dayText) };
    return isValidDate(date.y, date.m, date.d) ? date : null;
}

// Same rules as C# JapaneseEraText.TryParse. An era year may run past the era's end (Heisei 32 = 2020),
// but a date before the era's first day is rejected.
export function matchWareki(text, eras = []) {
    let era = null;
    for (const candidate of eras) {
        if (text.startsWith(candidate.name) && (!era || candidate.name.length > era.name.length)) {
            era = candidate;
        }
    }

    let parts = null;
    if (era) {
        const m = /^(元|[0-9]{1,2})(?:年([0-9]{1,2})月([0-9]{1,2})日|[/.-]([0-9]{1,2})[/.-]([0-9]{1,2}))$/.exec(text.slice(era.name.length));
        parts = m && [m[1] === '元' ? 1 : Number(m[1]), Number(m[2] ?? m[4]), Number(m[3] ?? m[5])];
    } else {
        era = eras.find(x => x.initial.toUpperCase() === text.charAt(0).toUpperCase()) ?? null;
        const m = era && /^([0-9]{1,2})[/.-]([0-9]{1,2})[/.-]([0-9]{1,2})$/.exec(text.slice(1));
        parts = m && [Number(m[1]), Number(m[2]), Number(m[3])];
    }

    if (!parts || parts[0] < 1) {
        return null;
    }

    const date = { y: Number(era.start.slice(0, 4)) + parts[0] - 1, m: parts[1], d: parts[2] };
    return isValidDate(date.y, date.m, date.d) && date.y <= 9999 && dateKey(date) >= era.start ? date : null;
}

function splitDate(text, reading) {
    const order = reading.order;
    const iso = YEAR_FIRST_DATE_TEXT.exec(text);
    if (iso) {
        return [iso[1], iso[2], iso[3]];
    }

    const full = (order === 'ymd' ? SHORT_YEAR_FIRST_DATE_TEXT : YEAR_LAST_DATE_TEXT).exec(text);
    if (full) {
        const [, a, b, c] = full;
        return order === 'ymd' ? [a, b, c] : order === 'mdy' ? [c, a, b] : [c, b, a];
    }

    const yearless = YEARLESS_DATE_TEXT.exec(text);
    if (yearless) {
        const [, a, b] = yearless;
        return order === 'dmy' ? [null, b, a] : [null, a, b];
    }

    if (DIGITS_DATE_TEXT.test(text)) {
        // ymd: yyyyMMdd, yyMMdd or MMdd; mdy: MMddyyyy, MMddyy or MMdd; dmy: ddMMyyyy, ddMMyy or ddMM
        const [front, back] = order === 'ymd' ? [text.slice(0, -4), text.slice(-4)] : [text.slice(4), text.slice(0, 4)];
        const [month, day] = order === 'dmy' ? [back.slice(2), back.slice(0, 2)] : [back.slice(0, 2), back.slice(2)];
        return [front.length === 0 ? null : front, month, day];
    }

    const kanji = reading.japanese ? KANJI_DATE_TEXT.exec(text) : null;
    return kanji ? [kanji[1] ?? null, kanji[2], kanji[3]] : null;
}

function fullYear(year) {
    return year < 30 ? 2000 + year : 1900 + year;
}

export function formatDate(date, reading = DEFAULT_READING) {
    return reading.editDate.replace(/yyyy|yy|MM|M|dd|d/g, token => {
        switch (token) {
            case 'yyyy':
                return String(date.y).padStart(4, '0');
            case 'yy':
                return pad2(date.y % 100);
            case 'MM':
                return pad2(date.m);
            case 'M':
                return String(date.m);
            case 'dd':
                return pad2(date.d);
            default:
                return String(date.d);
        }
    });
}

// Built on setUTCFullYear: day overflow rolls into the next month and years below 100 are not mapped to 19xx
export function addDays(date, days) {
    const t = utcDate(date.y, date.m, date.d + days);
    return { y: t.getUTCFullYear(), m: t.getUTCMonth() + 1, d: t.getUTCDate() };
}

function utcDate(y, m, d) {
    const t = new Date(0);
    t.setUTCFullYear(y, m - 1, d);
    return t;
}

export function addMonths(date, months) {
    const index = date.y * 12 + (date.m - 1) + months;
    const y = Math.floor(index / 12);
    const m = index - y * 12 + 1;
    return { y, m, d: Math.min(date.d, daysInMonth(y, m)) };
}

export function monthGrid(y, m, firstDay = 0) {
    const first = { y, m, d: 1 };
    const start = addDays(first, -((dayOfWeek(first) - firstDay + 7) % 7));
    return Array.from({ length: 42 }, (_, i) => addDays(start, i));
}

export function formatYearMonth(pattern, y, m, monthNames) {
    return pattern.replace(/'([^']*)'|yyyy|yy|MMMM|MMM|MM|M/g, (token, quoted) => {
        switch (token) {
            case 'yyyy':
                return String(y);
            case 'yy':
                return pad2(y % 100);
            case 'MMMM':
                return monthNames[m - 1] ?? String(m);
            case 'MMM':
                return (monthNames[m - 1] ?? String(m)).slice(0, 3);
            case 'MM':
                return pad2(m);
            case 'M':
                return String(m);
            default:
                return quoted;
        }
    });
}

function dayOfWeek(date) {
    return utcDate(date.y, date.m, date.d).getUTCDay();
}

function dateKey(date) {
    return `${String(date.y).padStart(4, '0')}-${pad2(date.m)}-${pad2(date.d)}`;
}

function parseKey(key) {
    const [y, m, d] = key.split('-').map(Number);
    return { y, m, d };
}

function sameDate(a, b) {
    return a.y === b.y && a.m === b.m && a.d === b.d;
}

function today() {
    const now = new Date();
    return { y: now.getFullYear(), m: now.getMonth() + 1, d: now.getDate() };
}

function inDateRange(date, rule) {
    const key = dateKey(date);
    const low = rule.minimum && BOUND_DATE.test(rule.minimum) ? rule.minimum : null;
    const high = rule.maximum && BOUND_DATE.test(rule.maximum) ? rule.maximum : null;
    return (!low || key >= low) && (!high || key <= high);
}

// ---- Movement rules ----

export function findNext(grid, row, col, dir, skip, wrap, accept = null) {
    let r = row;
    let c = col;
    // Leaving a merged range starts from its far edge in the direction of travel;
    // entering one stops at its top-left cell.
    const toEdge = merge => {
        if (dir === 'down') {
            r = merge.bottom;
        } else if (dir === 'up') {
            r = merge.top;
        } else if (dir === 'right') {
            c = merge.right;
        } else {
            c = merge.left;
        }
    };
    const start = grid.mergeAt?.(row, col) ?? null;
    if (start) {
        toEdge(start);
    }

    const limit = grid.rows * grid.cols;
    for (let guard = 0; guard < limit; guard++) {
        switch (dir) {
            case 'up':
                r--;
                break;
            case 'down':
                r++;
                break;
            case 'left':
                c--;
                if (c < 0 && wrap) {
                    c = grid.cols - 1;
                    r--;
                }
                break;
            default:
                c++;
                if (c >= grid.cols && wrap) {
                    c = 0;
                    r++;
                }
                break;
        }

        if (r < 0 || r >= grid.rows || c < 0 || c >= grid.cols) {
            return null;
        }

        let target = { r, c };
        const merge = grid.mergeAt?.(r, c) ?? null;
        if (merge) {
            // With row wrapping, only a merge's top-left cell is a stop; its other cells are skipped
            const anchor = r === merge.top && c === merge.left;
            if (!anchor && wrap) {
                continue;
            }

            target = { r: merge.top, c: merge.left };
            if (!wrap) {
                toEdge(merge);
            }
        }

        if (accept && !accept(target.r, target.c)) {
            continue;
        }

        if (!skip || !grid.isDisabled(target.r, target.c)) {
            return target;
        }
    }

    return null;
}

export function firstInputCell(grid) {
    for (let r = 0; r < grid.rows; r++) {
        for (let c = 0; c < grid.cols; c++) {
            if (!grid.isDisabled(r, c)) {
                return { r, c };
            }
        }
    }

    return { r: 0, c: 0 };
}

// ---- Ranges and clipboard tables ----

export function rangeOf(a, b) {
    return {
        top: Math.min(a.r, b.r),
        left: Math.min(a.c, b.c),
        bottom: Math.max(a.r, b.r),
        right: Math.max(a.c, b.c),
    };
}

export function nextInRange(grid, range, row, col, order, back, skip) {
    const height = range.bottom - range.top + 1;
    const width = range.right - range.left + 1;
    const count = height * width;
    let index = order === 'row'
        ? (row - range.top) * width + (col - range.left)
        : (col - range.left) * height + (row - range.top);
    for (let step = 1; step < count; step++) {
        index = (index + (back ? count - 1 : 1)) % count;
        const r = range.top + (order === 'row' ? Math.floor(index / width) : index % height);
        const c = range.left + (order === 'row' ? index % width : Math.floor(index / height));
        if (grid.isCovered?.(r, c)) {
            continue;
        }
        if (!skip || !grid.isDisabled(r, c)) {
            return { r, c };
        }
    }

    return null;
}

export function nextInGroup(grid, group, row, col, order, back, skip) {
    const box = grid.groupBounds.get(group);
    if (!box) {
        return null;
    }

    const cells = {
        isCovered: (r, c) => grid.isCovered(r, c) || grid.groupAt(r, c) !== group,
        isDisabled: (r, c) => grid.isDisabled(r, c),
    };
    return nextInRange(cells, box, row, col, order, back, skip);
}

function groupBounds(rules, patterns, rowPatterns) {
    const spans = patterns.map(pattern => {
        const found = new Map();
        pattern.forEach((index, c) => {
            const group = rules[index].group;
            if (!group) {
                return;
            }

            const span = found.get(group);
            if (span) {
                span.right = c;
            } else {
                found.set(group, { left: c, right: c });
            }
        });
        return found;
    });

    const bounds = new Map();
    rowPatterns.forEach((p, r) => {
        for (const [group, span] of spans[p]) {
            const box = bounds.get(group);
            if (box) {
                box.bottom = r;
                box.left = Math.min(box.left, span.left);
                box.right = Math.max(box.right, span.right);
            } else {
                bounds.set(group, { top: r, left: span.left, bottom: r, right: span.right });
            }
        }
    });
    return bounds;
}

export function indexAt(totals, offset) {
    let low = 0;
    let high = totals.length - 2;
    if (high < 0) {
        return 0;
    }

    while (low < high) {
        const mid = (low + high + 1) >> 1;
        if (totals[mid] <= offset) {
            low = mid;
        } else {
            high = mid - 1;
        }
    }
    return low;
}

// Same rules as C# ClipboardFormat.Parse. Excel ends the last row with a newline too,
// so exactly one trailing newline is stripped.
export function parseClipboard(text) {
    if (!text) {
        return [];
    }

    const source = text.replace(/(\r\n|\n|\r)$/, '');
    const rows = [];
    let row = [];
    let index = 0;
    for (;;) {
        const [cell, next] = readCell(source, index);
        row.push(cell);
        if (next >= source.length) {
            rows.push(row);
            return rows;
        }

        if (source[next] === '\t') {
            index = next + 1;
            continue;
        }

        rows.push(row);
        row = [];
        index = next + (source[next] === '\r' && source[next + 1] === '\n' ? 2 : 1);
    }
}

function readCell(text, index) {
    if (text[index] === '"') {
        const quoted = readQuoted(text, index);
        if (quoted) {
            return quoted;
        }
    }

    const rest = text.substring(index);
    const end = rest.search(/[\t\r\n]/);
    return end < 0 ? [rest, text.length] : [rest.substring(0, end), index + end];
}

function readQuoted(text, index) {
    let value = '';
    for (let i = index + 1; i < text.length; i++) {
        if (text[i] !== '"') {
            value += text[i];
            continue;
        }

        if (text[i + 1] === '"') {
            value += '"';
            i++;
            continue;
        }

        const next = i + 1;
        return next >= text.length || /[\t\r\n]/.test(text[next]) ? [value, next] : null;
    }

    return null;
}

export function clipboardCell(text, multiline = false) {
    const rows = parseClipboard(text);
    return rows.length ? normalizeLines(rows[0][0], multiline) : '';
}

// toTsv and toHtml must match C# ClipboardFormat, which builds the text for ranges
// that include rows not rendered here.
export function toTsv(rows) {
    return rows.map(row => row.map(quoteCell).join('\t') + '\r\n').join('');
}

function quoteCell(cell) {
    return /[\t\r\n]/.test(cell) || cell.startsWith('"') ? `"${cell.replace(/"/g, '""')}"` : cell;
}

export function toHtml(rows, isText) {
    const body = rows.map((row, r) => `<tr>${row.map((cell, c) => `${isText(r, c) ? TEXT_CELL : '<td>'}${escapeHtml(cell).replace(/\r\n|\n|\r/g, '<br>')}</td>`).join('')}</tr>`).join('');
    return `${EXCEL_STYLE}<table>${body}</table>`;
}

function escapeHtml(text) {
    return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function isTextKind(kind) {
    return kind === 'text' || kind === 'mask' || kind === 'autocomplete' || kind === 'list';
}

// ---- Context menu ----

export function menuKind(range, size) {
    if (range.top === range.bottom && range.left === range.right) {
        return 'cell';
    }

    const rows = range.left === 0 && range.right === size.cols - 1;
    const cols = range.top === 0 && range.bottom === size.rows - 1;
    if (rows && !cols) {
        return 'row';
    }
    return cols && !rows ? 'column' : 'cell';
}

export function menuEntries(kind, range, size, options, editable, messages) {
    const rows = range.bottom - range.top + 1;
    const cols = range.right - range.left + 1;
    const groups = [
        [
            { action: 'cut', label: messages.menuCut, key: `${SHORTCUT_PREFIX}X`, disabled: !editable },
            { action: 'copy', label: messages.menuCopy, key: `${SHORTCUT_PREFIX}C`, disabled: false },
            { action: 'paste', label: messages.menuPaste, key: `${SHORTCUT_PREFIX}V`, disabled: !editable },
        ],
        kind === 'column' ? [] : [
            options.allowInsertRows && { action: 'insertRowsAbove', label: fill(messages.menuInsertRowsAbove, rows), disabled: false },
            options.allowInsertRows && { action: 'insertRowsBelow', label: fill(messages.menuInsertRowsBelow, rows), disabled: false },
            options.allowDeleteRows && { action: 'deleteRows', label: fill(messages.menuDeleteRows, rows), disabled: rows >= size.rows },
        ],
        kind === 'row' ? [] : [
            options.allowInsertColumns && { action: 'insertColumnsLeft', label: fill(messages.menuInsertColumnsLeft, cols), disabled: false },
            options.allowInsertColumns && { action: 'insertColumnsRight', label: fill(messages.menuInsertColumnsRight, cols), disabled: false },
            options.allowDeleteColumns && { action: 'deleteColumns', label: fill(messages.menuDeleteColumns, cols), disabled: cols >= size.cols },
        ],
        (options.menuItems ?? []).map(x => ({ action: 'app', name: x.value, label: x.text, disabled: false })),
    ];

    const hidden = new Set((options.hiddenMenuItems ?? []).map(x => x.toLowerCase()));
    const entries = [];
    for (const group of groups) {
        const shown = group.filter(x => x && (x.action === 'app' || !hidden.has(x.action.toLowerCase())));
        if (shown.length) {
            if (entries.length) {
                entries.push(null);
            }
            entries.push(...shown);
        }
    }
    return entries;
}

export function nextMenuIndex(entries, index, step) {
    const n = entries.length;
    let start = index;
    if (index < 0) {
        start = step > 0 ? -1 : n;
    }

    for (let i = 1; i <= n; i++) {
        const next = (((start + step * i) % n) + n) % n;
        if (entries[next] && !entries[next].disabled) {
            return next;
        }
    }
    return -1;
}

// ---- Frozen rows and columns ----

// Pane ids: main is the scrolling area, l the frozen columns, t the frozen rows, tl where both overlap
export function paneOf(r, c, frozenRows, frozenCols) {
    if (r < frozenRows) {
        return c < frozenCols ? 'tl' : 't';
    }
    return c < frozenCols ? 'l' : 'main';
}

export function selectionPieces(range, frozenRows, frozenCols) {
    const rowBands = [[range.top, Math.min(range.bottom, frozenRows - 1)], [Math.max(range.top, frozenRows), range.bottom]];
    const colBands = [[range.left, Math.min(range.right, frozenCols - 1)], [Math.max(range.left, frozenCols), range.right]];
    const pieces = [];
    for (const [top, bottom] of rowBands) {
        for (const [left, right] of colBands) {
            if (top <= bottom && left <= right) {
                pieces.push({
                    pane: paneOf(top, left, frozenRows, frozenCols),
                    top,
                    left,
                    bottom,
                    right,
                    edges: { top: top === range.top, left: left === range.left, bottom: bottom === range.bottom, right: right === range.right },
                });
            }
        }
    }
    return pieces;
}

export function fillTarget(source, at) {
    const down = at.r - source.bottom;
    const up = source.top - at.r;
    const right = at.c - source.right;
    const left = source.left - at.c;
    const rows = Math.max(down, up, 0);
    const cols = Math.max(right, left, 0);
    if (rows === 0 && cols === 0) {
        return null;
    }

    if (rows >= cols) {
        return down > 0 ? { ...source, bottom: at.r } : { ...source, top: at.r };
    }

    return right > 0 ? { ...source, right: at.c } : { ...source, left: at.c };
}

function sameRange(a, b) {
    return a === b || (a !== null && b !== null && a.top === b.top && a.left === b.left && a.bottom === b.bottom && a.right === b.right);
}

export function frozenIndex(totals, frozen, onScreen, inContent) {
    if (frozen > 0 && onScreen < totals[frozen]) {
        return Math.min(indexAt(totals, onScreen), frozen - 1);
    }
    return Math.min(Math.max(indexAt(totals, inContent), frozen), totals.length - 2);
}

// moveBefore keeps focus on the moved element where supported; otherwise it is re-focused after the move
function moveInto(container, element) {
    if (element.parentElement === container) {
        return;
    }

    const focused = document.activeElement === element;
    let moved = false;
    if (typeof container.moveBefore === 'function') {
        try {
            container.moveBefore(element, null);
            moved = true;
        } catch {
            moved = false;
        }
    }

    if (!moved) {
        container.append(element);
    }

    if (focused && document.activeElement !== element) {
        element.focus({ preventScroll: true });
    }
}

// ---- Sheet information ----

export class SheetMap {
    constructor(map) {
        this.rows = map.rows;
        this.cols = map.columns;
        this.headerHeight = map.headerHeight;
        this.rowHeaderWidth = map.rowHeaderWidth;
        this.defaultRowHeight = map.defaultRowHeight;
        this.rules = map.rules;
        this.patterns = map.patterns;
        this.rowPatterns = map.rowPatterns;
        this.messages = map.messages;
        this.calendar = map.calendar ?? DEFAULT_CALENDAR;
        this.reading = { ...(map.reading ?? DEFAULT_READING), eras: map.eras ?? [] };
        this.colLefts = runningTotals(map.columnWidths);
        const heights = new Array(this.rows).fill(map.defaultRowHeight);
        for (let i = 0; i + 1 < map.customRowHeights.length; i += 2) {
            heights[map.customRowHeights[i]] = map.customRowHeights[i + 1];
        }
        this.rowTops = runningTotals(heights);

        this.merges = [];
        this.mergeIndex = new Map();
        const merges = map.merges ?? [];
        for (let i = 0; i + 3 < merges.length; i += 4) {
            const merge = { top: merges[i], left: merges[i + 1], bottom: merges[i + 2], right: merges[i + 3] };
            this.merges.push(merge);
            for (let r = merge.top; r <= merge.bottom; r++) {
                for (let c = merge.left; c <= merge.right; c++) {
                    this.mergeIndex.set(r * this.cols + c, merge);
                }
            }
        }

        this.groupBounds = groupBounds(this.rules, this.patterns, this.rowPatterns);
    }

    get isEmpty() {
        return this.rows === 0 || this.cols === 0;
    }

    rule(r, c) {
        return this.rules[this.patterns[this.rowPatterns[r]][c]];
    }

    isDisabled(r, c) {
        return this.isCovered(r, c) || this.rule(r, c).disabled;
    }

    groupAt(r, c) {
        return this.rule(r, c).group ?? null;
    }

    mergeAt(r, c) {
        return this.mergeIndex.get(r * this.cols + c) ?? null;
    }

    isCovered(r, c) {
        const merge = this.mergeAt(r, c);
        return merge !== null && (merge.top !== r || merge.left !== c);
    }

    anchorOf(r, c) {
        const merge = this.mergeAt(r, c);
        return merge ? { r: merge.top, c: merge.left } : { r, c };
    }

    expandRange(range) {
        let { top, left, bottom, right } = range;
        let changed = this.merges.length > 0;
        while (changed) {
            changed = false;
            for (const m of this.merges) {
                const hits = m.top <= bottom && m.bottom >= top && m.left <= right && m.right >= left;
                if (hits && (m.top < top || m.left < left || m.bottom > bottom || m.right > right)) {
                    top = Math.min(top, m.top);
                    left = Math.min(left, m.left);
                    bottom = Math.max(bottom, m.bottom);
                    right = Math.max(right, m.right);
                    changed = true;
                }
            }
        }
        return { top, left, bottom, right };
    }

    cellRect(r, c) {
        const merge = this.mergeAt(r, c);
        if (!merge) {
            return this.cellBox(r, c);
        }

        const first = this.cellBox(merge.top, merge.left);
        const last = this.cellBox(merge.bottom, merge.right);
        return { x: first.x, y: first.y, w: last.x + last.w - first.x, h: last.y + last.h - first.y };
    }

    cellBox(r, c) {
        return {
            x: this.rowHeaderWidth + this.colLefts[c],
            y: this.headerHeight + this.rowTops[r],
            w: this.colLefts[c + 1] - this.colLefts[c],
            h: this.rowTops[r + 1] - this.rowTops[r],
        };
    }
}

function runningTotals(sizes) {
    const totals = new Array(sizes.length + 1);
    totals[0] = 0;
    for (let i = 0; i < sizes.length; i++) {
        totals[i + 1] = totals[i] + sizes[i];
    }
    return totals;
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

function createElement(tag, className) {
    const element = document.createElement(tag);
    element.className = className;
    return element;
}

function createButton(className, text, label) {
    const button = createElement('button', className);
    button.type = 'button';
    button.tabIndex = -1;
    button.textContent = text;
    if (label) {
        button.setAttribute('aria-label', label);
    }
    return button;
}

// Must produce the same notation as C# KeyCombo
export function comboOf(k) {
    const key = k.key === ' ' ? 'Space' : k.key.length === 1 ? k.key.toUpperCase() : k.key;
    return `${k.ctrl ? 'Ctrl+' : ''}${k.alt ? 'Alt+' : ''}${k.shift ? 'Shift+' : ''}${key}`;
}

function keyInfo(e) {
    return { key: e.key, shift: e.shiftKey, ctrl: e.ctrlKey || e.metaKey, alt: e.altKey, replay: false };
}

function showFloating(element) {
    element.classList.add('mussel-sheet-open');
    if (SUPPORTS_POPOVER && element.popover && !element.matches(':popover-open')) {
        element.showPopover();
    }
}

function hideFloating(element) {
    element.classList.remove('mussel-sheet-open');
    if (SUPPORTS_POPOVER && element.popover && element.isConnected && element.matches(':popover-open')) {
        element.hidePopover();
    }
}

// ---- Input handling ----

class SheetController {
    constructor(root, dotnet, options, map) {
        this.root = root;
        this.dotnet = dotnet;
        this.viewport = root.querySelector('.mussel-sheet-viewport');
        this.content = root.querySelector('.mussel-sheet-content');
        this.layer = root.querySelector('.mussel-sheet-layer');
        this.opts = { ...DEFAULT_OPTIONS, ...options };
        this.map = new SheetMap(map);
        this.state = READY;
        this.row = 0;
        this.col = 0;
        this.anchor = { r: 0, c: 0 };
        this.extent = { r: 0, c: 0 };
        this.drag = null;
        this.tabStartCol = -1;
        this.queue = [];
        this.compositionEndAt = -Infinity;
        this.disposed = false;
        this.activeTimer = 0;
        this.notified = null;
        this.covered = { top: -Infinity, bottom: Infinity, left: -Infinity, right: Infinity };
        this.viewBusy = false;
        this.viewPending = false;
        this.lastSend = Promise.resolve();

        this.popupKind = null;
        this.popupItems = [];
        this.popupIndex = -1;
        this.calendarNav = false;
        this.calSelected = null;
        this.calFocus = null;
        this.calMonth = null;
        this.suggestTimer = 0;
        this.suggestSeq = 0;

        this.custom = null;
        this.customHandlers = null;
        this.customBusy = false;

        this.menuOpen = false;
        this.menuItems = [];
        this.menuIndex = -1;

        // JS-owned elements live in containers whose content Blazor never renders (.mussel-sheet-layer).
        // Elements of frozen rows and columns go in containers that stick to their pane; all use sheet coordinates.
        // decor: frame, selection, drop arrow (inside the grid for frozen rows); input: sink, messages (outside it).
        this.top = root.querySelector('.mussel-sheet-top');
        const paneLayer = selector => root.querySelector(selector) ?? this.layer;
        this.decorLayers = { main: this.layer, l: paneLayer('.mussel-sheet-pane-l'), t: paneLayer('.mussel-sheet-pane-t'), tl: paneLayer('.mussel-sheet-pane-tl') };
        this.inputLayers = { main: this.layer, l: paneLayer('.mussel-sheet-pane-l'), t: paneLayer('.mussel-sheet-pane-ts'), tl: paneLayer('.mussel-sheet-pane-tls') };

        this.selections = {};
        for (const [pane, layer] of Object.entries(this.decorLayers)) {
            const piece = createElement('div', 'mussel-sheet-selection');
            piece.hidden = true;
            layer.append(piece);
            this.selections[pane] = piece;
        }
        this.fillRanges = {};
        for (const [pane, layer] of Object.entries(this.decorLayers)) {
            const piece = createElement('div', 'mussel-sheet-fill-range');
            piece.hidden = true;
            layer.append(piece);
            this.fillRanges[pane] = piece;
        }
        this.probe = createElement('div', 'mussel-sheet-probe');
        this.frame = createElement('div', 'mussel-sheet-active');
        this.handle = createElement('div', 'mussel-sheet-fill');
        this.handle.hidden = true;
        this.handle.setAttribute('aria-hidden', 'true');
        this.drop = createElement('div', 'mussel-sheet-drop');
        this.drop.hidden = true;
        this.drop.setAttribute('aria-hidden', 'true');
        this.tip = createElement('div', 'mussel-sheet-tip');
        this.tip.hidden = true;
        this.capTop = createElement('div', 'mussel-sheet-cap');
        this.capTop.hidden = true;
        this.capLeft = createElement('div', 'mussel-sheet-cap');
        this.capLeft.hidden = true;
        this.note = createElement('div', 'mussel-sheet-note');
        this.note.hidden = true;
        this.note.id = `mussel-sheet-note-${++noteSequence}`;
        this.note.setAttribute('role', 'tooltip');
        this.popup = createElement('div', 'mussel-sheet-popup');
        if (SUPPORTS_POPOVER) {
            this.popup.popover = 'manual';
        }
        this.menu = createElement('div', 'mussel-sheet-menu');
        this.menu.setAttribute('role', 'menu');
        if (SUPPORTS_POPOVER) {
            this.menu.popover = 'manual';
        }
        this.sink = this.createSink();
        this.layer.append(this.probe, this.frame, this.drop, this.handle, this.sink, this.tip, this.note, this.popup, this.menu);
        this.inputLayers.t.append(this.capTop);
        this.decorLayers.l.append(this.capLeft);
        this.pane = 'main';

        this.listeners = [];
        this.listen(this.sink, 'keydown', e => this.onKeyDown(e));
        this.listen(this.sink, 'input', e => this.onInput(e));
        this.listen(this.sink, 'compositionstart', () => this.onCompositionStart());
        this.listen(this.sink, 'compositionend', () => this.onCompositionEnd());
        this.listen(this.sink, 'blur', e => this.onBlur(e));
        this.listen(this.sink, 'focus', () => this.syncNote());
        this.listen(this.sink, 'copy', e => this.onCopy(e));
        this.listen(this.sink, 'cut', e => this.onCut(e));
        this.listen(this.sink, 'paste', e => this.onPaste(e));
        this.listen(this.viewport, 'mousedown', e => this.onMouseDown(e));
        this.listen(this.viewport, 'dblclick', e => this.onDoubleClick(e));
        this.listen(this.viewport, 'scroll', () => this.onScroll(), { passive: true });
        this.listen(this.popup, 'mousedown', e => this.onPopupMouseDown(e));
        this.listen(this.popup, 'mouseover', e => this.onPopupMouseOver(e));
        this.listen(this.viewport, 'contextmenu', e => this.onContextMenu(e));
        this.listen(this.menu, 'mousedown', e => e.preventDefault());
        this.listen(this.menu, 'click', e => this.onMenuClick(e));
        this.listen(this.menu, 'mouseover', e => this.onMenuMouseOver(e));
        this.listen(document, 'mousedown', e => this.onDocumentMouseDown(e), true);
        this.listen(window, 'blur', () => this.closeMenu());
        this.listen(window, 'scroll', () => this.placeFloatings(), { capture: true, passive: true });
        this.listen(window, 'resize', () => {
            this.closeMenu();
            this.placeFloatings();
        }, { passive: true });
        // ResizeObserver also fires when observation starts, which reports the initial viewport height
        this.observer = new ResizeObserver(() => this.checkViewport(true));
        this.observer.observe(this.viewport);

        this.setState(READY);
        const first = firstInputCell(this.map);
        this.moveTo(first.r, first.c, { scroll: false });
    }

    // ---- Called from C# ----

    setMap(map) {
        this.closeMenu();
        this.map = new SheetMap(map);
        if (this.map.isEmpty) {
            this.finishEdit();
            this.place();
            return;
        }

        this.row = clamp(this.row, 0, this.map.rows - 1);
        this.col = clamp(this.col, 0, this.map.cols - 1);
        this.anchor = this.clampCell(this.anchor);
        this.extent = this.clampCell(this.extent);
        if (this.isEditing() && this.map.isDisabled(this.row, this.col)) {
            this.finishEdit();
        }
        this.place();
        this.updateSink();
    }

    setOptions(options) {
        this.opts = { ...this.opts, ...options };
        this.place();
    }

    setCovered(top, bottom, left, right) {
        this.covered = { top, bottom, left, right };
        this.checkViewport(false);
    }

    focus() {
        this.sink.focus({ preventScroll: true });
    }

    // Browser UTC offset in minutes. C# decides 'this year' for short date input from the browser's date,
    // even on Blazor Server, so it needs the offset.
    timezoneOffset() {
        return new Date().getTimezoneOffset();
    }

    setActiveCell(row, col) {
        if (this.map.isEmpty) {
            return;
        }

        const r = clamp(row, 0, this.map.rows - 1);
        const c = clamp(col, 0, this.map.cols - 1);
        if (this.state === CUSTOM) {
            this.commitCustom(() => this.moveTo(r, c));
        } else if (this.isEditing()) {
            this.commit(() => this.moveTo(r, c));
        } else if (this.state === READY) {
            this.moveTo(r, c);
        }
    }

    setSelection(top, left, bottom, right) {
        if (this.map.isEmpty) {
            return;
        }

        const apply = () => this.select({ r: top, c: left }, { r: bottom, c: right });
        if (this.state === CUSTOM) {
            this.commitCustom(apply);
        } else if (this.isEditing()) {
            this.commit(apply);
        } else if (this.state === READY) {
            apply();
        }
    }

    beginEdit() {
        if (this.state !== READY) {
            return;
        }

        const rule = this.currentRule();
        if (this.canEdit(rule)) {
            this.startEdit();
        } else if (this.canOpenCustom(rule)) {
            this.openCustom();
        }
    }

    commitEdit() {
        if (this.state === CUSTOM) {
            this.commitCustom(null);
        } else if (this.isEditing()) {
            this.commit(null);
        }
    }

    cancelEdit() {
        if (this.state === CUSTOM) {
            this.cancelCustom();
        } else if (this.isEditing()) {
            this.finishEdit();
        }
    }

    dispose() {
        this.disposed = true;
        clearTimeout(this.activeTimer);
        clearTimeout(this.suggestTimer);
        for (const [target, type, handler, options] of this.listeners) {
            target.removeEventListener(type, handler, options);
        }
        this.detachCustom();
        this.endDrag();
        this.observer.disconnect();
        hideFloating(this.popup);
        hideFloating(this.menu);
        this.menu.remove();
        for (const piece of Object.values(this.selections)) {
            piece.remove();
        }
        this.probe.remove();
        this.frame.remove();
        this.drop.remove();
        this.sink.remove();
        this.tip.remove();
        this.note.remove();
        this.capTop.remove();
        this.capLeft.remove();
        this.popup.remove();
    }

    // ---- Input sink ----

    // Focus stays on a transparent textarea even while ready, so typing, including IME composition,
    // starts in the element that then becomes the editor.
    createSink() {
        const sink = document.createElement('textarea');
        sink.className = 'mussel-sheet-sink';
        sink.rows = 1;
        sink.wrap = 'off';
        sink.spellcheck = false;
        sink.setAttribute('autocomplete', 'off');
        sink.setAttribute('autocorrect', 'off');
        sink.setAttribute('autocapitalize', 'off');
        sink.setAttribute('aria-label', this.map.messages.cellInput ?? '');
        return sink;
    }

    listen(target, type, handler, options) {
        target.addEventListener(type, handler, options);
        this.listeners.push([target, type, handler, options]);
    }

    onKeyDown(e) {
        // Leave keys to the IME while composing; test both isComposing and keyCode 229
        if (e.isComposing || e.keyCode === 229) {
            this.closeMenu();
            return;
        }

        if (this.drag?.mode === 'fill' && e.key === 'Escape') {
            e.preventDefault();
            this.endDrag();
            return;
        }

        if (this.menuOpen && this.menuKey(keyInfo(e))) {
            e.preventDefault();
            return;
        }

        if (e.key === 'Enter' && performance.now() - this.compositionEndAt < COMPOSITION_ENTER_GUARD_MS) {
            e.preventDefault();
            return;
        }

        // Keys typed while C# is committing are queued and replayed after the move,
        // including Ctrl+Z and Ctrl+Y right after a row insert.
        if (this.state === COMMITTING) {
            const k = keyInfo(e);
            const history = k.ctrl && !k.alt && (k.key.toLowerCase() === 'z' || k.key.toLowerCase() === 'y');
            if (QUEUED_KEYS.has(k.key) || (k.key.length === 1 && !k.ctrl && !k.alt) || history) {
                e.preventDefault();
                this.queue.push(k);
            }
            return;
        }

        if (this.handleKey(keyInfo(e))) {
            e.preventDefault();
        }
    }

    onInput(e) {
        if (this.state === READY) {
            if (!this.canEdit(this.currentRule())) {
                this.sink.value = '';
                return;
            }

            this.setState(INPUT);
            this.showEditor();
            this.onEditorInput(e);
            return;
        }

        this.fit();
        this.onEditorInput(e);
    }

    onEditorInput(e) {
        if (!this.isEditing()) {
            return;
        }

        switch (this.currentRule().kind) {
            case 'list':
                this.openList(false);
                break;
            case 'date':
                if (this.popupKind === 'calendar') {
                    this.followCalendar();
                }
                break;
            case 'autocomplete':
                if (!e?.isComposing) {
                    this.scheduleSuggest();
                }
                break;
            default:
                break;
        }
    }

    // Composition can start while ready, so only switch state here;
    // recreating the element or moving focus would break the IME composition.
    onCompositionStart() {
        if (this.state === READY && this.canEdit(this.currentRule())) {
            this.setState(INPUT);
            this.showEditor();
        }
    }

    onCompositionEnd() {
        this.compositionEndAt = performance.now();
        if (this.isEditing() && this.currentRule().kind === 'autocomplete') {
            this.scheduleSuggest();
        }
    }

    onBlur(e) {
        this.closeMenu();
        if (this.isEditing() && e.relatedTarget && !this.root.contains(e.relatedTarget)) {
            this.commit(null, true);
        }
        this.syncNote();
    }

    // ---- Copy, cut and paste ----

    onCopy(e) {
        if (this.state !== READY || this.map.isEmpty) {
            return;
        }

        e.preventDefault();
        if (this.hasRange()) {
            this.copyRange(e);
        } else {
            this.copyCell(e);
        }
    }

    onCut(e) {
        if (this.state !== READY || this.map.isEmpty) {
            return;
        }

        e.preventDefault();
        if (this.hasRange()) {
            this.copyRange(e);
        } else {
            this.copyCell(e);
        }
        this.cutSelection();
    }

    cutSelection() {
        if (this.hasRange()) {
            const range = this.selectionRange();
            this.send('CutRangeAsync', range.top, range.left, range.bottom, range.right);
        } else if (!this.currentRule().disabled) {
            this.send('CutAsync', this.row, this.col);
        }
    }

    onPaste(e) {
        if (this.state === CUSTOM) {
            return;
        }

        e.preventDefault();
        const text = e.clipboardData?.getData('text/plain') ?? '';
        if (this.isEditing()) {
            this.insertText(clipboardCell(text, this.currentRule().multiline));
            return;
        }

        this.pasteText(text);
    }

    pasteText(text) {
        if (this.state !== READY || this.map.isEmpty || !text) {
            return;
        }

        const table = parseClipboard(text);
        if (!table.length) {
            return;
        }

        if (this.hasRange() || table.length > 1 || table[0].length > 1) {
            this.pasteRange(text);
        } else {
            this.paste(clipboardCell(text, this.currentRule().multiline));
        }
    }

    // Copies the unformatted edit text, not the displayed text. Even a single cell gets an HTML table,
    // so Excel keeps the leading zeros of text cells.
    copyCell(e) {
        const range = { top: this.row, left: this.col };
        const cell = this.cellElement(this.row, this.col);
        if (cell) {
            const text = cell.dataset.edit ?? cell.textContent;
            e.clipboardData.setData('text/plain', text);
            e.clipboardData.setData('text/html', this.htmlOf([[text]], range));
            return;
        }

        const data = this.dotnet.invokeMethodAsync('GetEditText', this.row, this.col)
            .then(text => ({ text, html: this.htmlOf([[text]], range) }))
            .catch(() => ({ text: '', html: '' }));
        this.writeClipboard(data, true);
    }

    // Rows outside the rendered window exist only in C#, so the text is fetched from it
    // and written to the clipboard afterwards.
    copyRange(e) {
        const range = this.selectionRange();
        const values = this.renderedValues(range);
        if (values) {
            e.clipboardData.setData('text/plain', toTsv(values));
            e.clipboardData.setData('text/html', this.htmlOf(values, range));
            return;
        }

        this.writeClipboard(this.rangeText(range), true);
    }

    htmlOf(values, range) {
        return toHtml(values, (r, c) => isTextKind(this.map.rule(range.top + r, range.left + c).kind));
    }

    rangeText(range) {
        return this.dotnet.invokeMethodAsync('GetRangeText', range.top, range.left, range.bottom, range.right).catch(() => ({ text: '', html: '' }));
    }

    renderedValues(range) {
        const values = [];
        for (let r = range.top; r <= range.bottom; r++) {
            const row = this.root.querySelector(`.mussel-sheet-row[data-r="${r}"]`);
            if (!row) {
                return null;
            }

            const cells = new Map([...row.querySelectorAll('.mussel-sheet-cell')].map(cell => [Number(cell.dataset.c), cell]));
            const line = [];
            for (let c = range.left; c <= range.right; c++) {
                const cell = cells.get(c);
                if (!cell) {
                    return null;
                }
                line.push(cell.dataset.edit ?? cell.textContent);
            }
            values.push(line);
        }
        return values;
    }

    // Promises go straight into ClipboardItem so the write starts inside the user gesture while the text
    // may still be coming from C#; without ClipboardItem only plain text is written.
    writeClipboard(data, html) {
        if (typeof ClipboardItem !== 'undefined' && navigator.clipboard?.write) {
            const item = { 'text/plain': data.then(d => new Blob([d.text], { type: 'text/plain' })) };
            if (html) {
                item['text/html'] = data.then(d => new Blob([d.html], { type: 'text/html' }));
            }
            navigator.clipboard.write([new ClipboardItem(item)]).catch(() => { });
        } else {
            data.then(d => navigator.clipboard?.writeText(d.text)).catch(() => { });
        }
    }

    // Sent as a stream so tables over the Interactive Server's 32 KB message limit still go through
    pasteRange(text) {
        const range = this.selectionRange();
        this.runAndSelect(
            () => this.dotnet.invokeMethodAsync('PasteRangeAsync', range.top, range.left, range.bottom, range.right, DotNet.createJSStreamReference(new TextEncoder().encode(text))),
            null);
    }

    async runAndSelect(call, active) {
        this.setState(COMMITTING);
        this.updateSink();
        let result = null;
        try {
            await this.lastSend;
            result = await call();
        } catch {
            result = null;
        }

        if (this.disposed) {
            return;
        }

        this.setState(READY);
        this.updateSink();
        if (result) {
            const anchor = { r: result.top, c: result.left };
            this.select(anchor, { r: result.bottom, c: result.right }, active ?? anchor);
            if (result.message) {
                this.notice(result.message);
            }
        } else {
            this.placeHandle();
        }
        this.replay();
    }

    runFill(source, whole, toggle) {
        const active = { r: this.row, c: this.col };
        this.runAndSelect(
            () => this.dotnet.invokeMethodAsync('AutoFillAsync', source.top, source.left, source.bottom, source.right, whole.top, whole.left, whole.bottom, whole.right, toggle),
            active);
    }

    // execCommand keeps the browser's own undo (Ctrl+Z) working; setRangeText is the fallback
    insertText(text) {
        if (!document.execCommand('insertText', false, text)) {
            this.sink.setRangeText(text, this.sink.selectionStart, this.sink.selectionEnd, 'end');
            this.fit();
            this.onEditorInput(null);
        }
    }

    async paste(text) {
        const row = this.row;
        const col = this.col;
        const rule = this.currentRule();
        if (rule.disabled) {
            return;
        }

        const result = checkInput(rule, text, this.map.messages, this.map.reading);
        if (!result.ok && rule.policy !== 'accept') {
            this.rejectPaste(rule, text, result.error);
            return;
        }

        if (this.opts.commitMode === 'optimistic' || !result.ok) {
            this.send('PasteAsync', row, col, text, true);
            return;
        }

        this.setState(COMMITTING);
        this.updateSink();
        let response;
        try {
            response = await this.dotnet.invokeMethodAsync('PasteAsync', row, col, text, false);
        } catch {
            response = { ok: false, error: this.map.messages.canceled, stored: false };
        }

        if (this.disposed) {
            return;
        }

        this.setState(READY);
        this.updateSink();
        if (response.ok || response.stored || this.row !== row || this.col !== col) {
            this.replay();
            return;
        }

        this.queue = [];
        this.rejectPaste(rule, text, response.error);
    }

    rejectPaste(rule, text, error) {
        if (rule.policy === 'stay' && this.canEdit(rule)) {
            this.startInput(text);
            this.showError(error);
        } else {
            this.notice(error);
        }
    }

    notice(message) {
        this.tip.textContent = message;
        this.tip.hidden = false;
        this.placeTip();
        this.syncNote();
    }

    // ---- Key handling ----

    // Keys assigned by the app (ReadyKeys, EditingKeys) are checked first. A null assignment unbinds the key:
    // the sheet ignores it and the browser handles it. Unlisted keys keep their built-in behavior.
    handleKey(k) {
        const table = this.state === READY ? this.opts.readyKeys : this.opts.editingKeys;
        const combo = comboOf(k);
        if (table && Object.hasOwn(table, combo)) {
            const command = table[combo];
            return command ? this.runCommand(command) : false;
        }

        return this.builtinKey(k);
    }

    builtinKey(k) {
        if (this.popupKind && this.isEditing() && this.popupKey(k)) {
            return true;
        }

        return this.state === READY ? this.readyKey(k) : this.editingKey(k);
    }

    runCommand(name) {
        const key = COMMAND_KEYS[name];
        return key ? this.builtinKey({ key: key.key, shift: !!key.shift, ctrl: !!key.ctrl, alt: !!key.alt, replay: false }) : false;
    }

    readyKey(k) {
        const rule = this.currentRule();
        this.hideTip();
        if (k.ctrl && !k.alt) {
            switch (k.key.toLowerCase()) {
                case 'z':
                    this.history(k.shift);
                    return true;
                case 'y':
                    this.history(true);
                    return true;
                case 'a':
                    this.selectAll();
                    return true;
                case 'd':
                    this.fillFrom('FillDownAsync');
                    return true;
                case 'r':
                    this.fillFrom('FillRightAsync');
                    return true;
                case ';':
                    this.enterToday();
                    return true;
                case 'enter':
                    this.fillWithActive();
                    return true;
                case 'home':
                    this.jumpHome(k.shift);
                    return true;
                case 'end':
                    this.jump('end', k.shift);
                    return true;
                case 'arrowup':
                case 'arrowdown':
                case 'arrowleft':
                case 'arrowright':
                    this.jump(k.key.substring(5).toLowerCase(), k.shift);
                    return true;
                default:
                    return false;
            }
        }

        if ((k.key === 'F10' && k.shift && !k.alt) || k.key === 'ContextMenu') {
            this.openMenuAtCell();
            return true;
        }

        if (k.shift && !k.alt && EXTEND_KEYS.has(k.key)) {
            this.extendByKey(k.key);
            return true;
        }

        if (k.alt && (k.key === 'ArrowDown' || k.key === 'ArrowUp')) {
            if (k.key === 'ArrowDown') {
                this.openDrop();
            }
            return true;
        }

        switch (k.key) {
            case 'ArrowUp':
                return this.arrow('up');
            case 'ArrowDown':
                return this.arrow('down');
            case 'ArrowLeft':
                return this.arrow('left');
            case 'ArrowRight':
                return this.arrow('right');
            case 'Enter':
                if (k.alt) {
                    // Alt+Enter only means a line break while typing, so it is swallowed here
                    return true;
                }
                if (this.opts.readyEnter === 'edit' && this.canEdit(rule)) {
                    this.startEdit();
                } else if (this.opts.readyEnter === 'edit' && this.canOpenCustom(rule)) {
                    this.openCustom();
                } else {
                    this.enterMove(k.shift);
                }
                return true;
            case 'Tab':
                return this.tabMove(k.shift);
            case 'Home':
                this.moveTo(this.row, 0);
                return true;
            case 'End':
                this.moveTo(this.row, this.map.cols - 1);
                return true;
            case 'PageUp':
                this.page(-1);
                return true;
            case 'PageDown':
                this.page(1);
                return true;
            case 'F2':
                if (this.canEdit(rule)) {
                    this.startEdit();
                } else if (this.canOpenCustom(rule)) {
                    this.openCustom();
                }
                return true;
            case 'Escape':
                return true;
            case 'Delete':
                if (this.hasRange()) {
                    const range = this.selectionRange();
                    this.send('ClearRangeAsync', range.top, range.left, range.bottom, range.right);
                } else if (!rule.disabled) {
                    this.send('ClearAsync', this.row, this.col);
                }
                return true;
            case 'Backspace':
                if (this.canEdit(rule)) {
                    this.startInput('');
                }
                return true;
            case ' ':
                if (rule.kind === 'check') {
                    if (!rule.disabled) {
                        this.send('ToggleAsync', this.row, this.col);
                    }
                    return true;
                }
                break;
            default:
                break;
        }

        if (k.replay && k.key.length === 1 && !k.ctrl && !k.alt) {
            // Replayed characters never reached the sink, so start input with them explicitly
            if (this.canEdit(rule)) {
                this.startInput(k.key);
            }
            return true;
        }

        // Printable keys keep their default action and land in the sink; the input event then starts input mode
        return false;
    }

    editingKey(k) {
        if (k.ctrl && !k.alt && k.key === ';') {
            this.insertText(formatDate(today(), this.map.reading));
            return true;
        }

        if (k.alt && (k.key === 'ArrowDown' || k.key === 'ArrowUp')) {
            if (k.key === 'ArrowDown') {
                this.openDrop();
            } else {
                this.closePopup();
            }
            return true;
        }

        switch (k.key) {
            case 'Escape':
                this.finishEdit();
                return true;
            case 'Enter':
                if (k.alt) {
                    if (this.currentRule().multiline) {
                        this.insertText('\n');
                    }
                    return true;
                }
                if (k.ctrl) {
                    this.commitInPlace();
                    return true;
                }
                this.commit(() => this.enterMove(k.shift));
                return true;
            case 'Tab':
                if (!this.hasRange() && this.currentGroup() === null && this.tabTarget(k.shift) === null && this.opts.rowEnd === 'leave') {
                    // Leaving the sheet: send the commit without waiting and let the browser move focus
                    this.commit(null, true);
                    return false;
                }
                this.commit(() => this.tabMove(k.shift));
                return true;
            case 'F2':
                this.setState(this.state === INPUT ? EDIT : INPUT);
                return true;
            case 'ArrowUp':
            case 'ArrowDown':
            case 'ArrowLeft':
            case 'ArrowRight':
                if (this.state === INPUT) {
                    const dir = k.key.substring(5).toLowerCase();
                    this.commit(() => {
                        if (k.ctrl) {
                            this.jump(dir, k.shift);
                        } else if (k.shift) {
                            this.extendByKey(k.key);
                        } else {
                            this.arrow(dir);
                        }
                    });
                    return true;
                }
                return false;
            case 'PageUp':
            case 'PageDown':
                if (this.state === INPUT) {
                    const step = k.key === 'PageUp' ? -1 : 1;
                    this.commit(() => this.page(step));
                }
                return true;
            default:
                break;
        }

        if (k.replay && k.key.length === 1 && !k.ctrl && !k.alt) {
            this.sink.setRangeText(k.key, this.sink.selectionStart, this.sink.selectionEnd, 'end');
            this.fit();
            this.onEditorInput(null);
            return true;
        }

        return false;
    }

    // Returns true when handled; Enter and Tab only insert the value and fall through to the normal commit
    popupKey(k) {
        if (k.ctrl) {
            return false;
        }

        if (k.alt) {
            if (k.key === 'ArrowUp') {
                this.closePopup();
                return true;
            }
            if (k.key === 'ArrowDown') {
                if (this.popupKind === 'calendar' && !this.calendarNav) {
                    this.openCalendar(true);
                } else {
                    this.closePopup();
                }
                return true;
            }
            return false;
        }

        if (this.popupKind === 'calendar') {
            return this.calendarKey(k);
        }

        const last = this.popupItems.length - 1;
        switch (k.key) {
            case 'ArrowDown':
                this.highlight(Math.min(this.popupIndex + 1, last));
                return true;
            case 'ArrowUp':
                this.highlight(Math.max(this.popupIndex - 1, this.popupKind === 'suggest' ? -1 : 0));
                return true;
            case 'PageDown':
                this.highlight(Math.min(Math.max(this.popupIndex, 0) + LIST_PAGE, last));
                return true;
            case 'PageUp':
                this.highlight(Math.max(this.popupIndex - LIST_PAGE, 0));
                return true;
            case 'Enter':
            case 'Tab':
                if (this.popupIndex >= 0) {
                    this.sink.value = this.popupItems[this.popupIndex].text;
                }
                return false;
            case 'Escape':
                this.closePopup();
                return true;
            default:
                return false;
        }
    }

    // Arrows pick a day only when the calendar was opened with Alt+Down;
    // the one shown automatically while editing leaves keys to normal editing.
    calendarKey(k) {
        if (!this.calendarNav) {
            return false;
        }

        const focus = this.calFocus ?? today();
        switch (k.key) {
            case 'ArrowLeft':
                this.moveCalendarFocus(addDays(focus, -1));
                return true;
            case 'ArrowRight':
                this.moveCalendarFocus(addDays(focus, 1));
                return true;
            case 'ArrowUp':
                this.moveCalendarFocus(addDays(focus, -7));
                return true;
            case 'ArrowDown':
                this.moveCalendarFocus(addDays(focus, 7));
                return true;
            case 'PageUp':
                this.moveCalendarFocus(addMonths(focus, -1));
                return true;
            case 'PageDown':
                this.moveCalendarFocus(addMonths(focus, 1));
                return true;
            case 'Enter':
            case 'Tab':
                if (inDateRange(focus, this.currentRule())) {
                    this.sink.value = formatDate(focus, this.map.reading);
                }
                return false;
            case 'Escape':
                this.closePopup();
                return true;
            default:
                return false;
        }
    }

    // ---- Movement ----

    arrow(dir) {
        const next = findNext(this.map, this.row, this.col, dir, this.opts.skipReadOnly === 'all', false);
        if (next) {
            this.moveTo(next.r, next.c);
        }
        return true;
    }

    enterMove(back, allowAdd = true) {
        const direction = this.opts.enterDirection;
        if (direction === 'none' || this.map.isEmpty) {
            return;
        }

        if (this.hasRange()) {
            this.moveInRange(direction === 'right' ? 'row' : 'col', back);
            return;
        }

        if (this.currentGroup() !== null) {
            this.moveInGroup(direction === 'right' ? 'row' : 'col', back);
            return;
        }

        const skip = this.opts.skipReadOnly !== 'none';
        const free = this.outsideGroups();
        let next;
        if (direction === 'down') {
            // Enter after Tabbing right returns to the column where the Tab run started, one row down (as in Excel)
            const col = !back && this.tabStartCol >= 0 ? this.tabStartCol : this.col;
            next = findNext(this.map, this.row, col, back ? 'up' : 'down', skip, false, free);
        } else {
            next = findNext(this.map, this.row, this.col, back ? 'left' : 'right', skip, this.opts.rowEnd === 'next', free);
        }

        // Only an input cell adds a row, never a read-only cell or label, even with no input cell below
        if (next) {
            this.moveTo(next.r, next.c);
        } else if (!back && allowAdd && this.opts.lastRowEnter === 'add' && !this.map.isDisabled(this.row, this.col)) {
            this.addRow();
        }
    }

    async addRow() {
        const row = this.row;
        const col = this.col;
        this.setState(COMMITTING);
        this.updateSink();
        let added = false;
        try {
            // Wait for the previous commit (sent without awaiting) so undo sees the commit first, then the row add
            await this.lastSend;
            added = await this.dotnet.invokeMethodAsync('AddRowFromScriptAsync', row, col);
        } catch {
            added = false;
        }

        if (this.disposed) {
            return;
        }

        this.setState(READY);
        this.updateSink();
        // C# sends the updated sheet map before returning, so the new row is already known here
        if (added && this.row === row && this.col === col) {
            this.enterMove(false, false);
        }
        this.replay();
    }

    tabTarget(back) {
        if (this.map.isEmpty) {
            return null;
        }

        const skip = this.opts.skipReadOnly !== 'none';
        const group = this.currentGroup();
        if (group !== null) {
            return nextInGroup(this.map, group, this.row, this.col, 'row', back, skip);
        }

        return findNext(this.map, this.row, this.col, back ? 'left' : 'right', skip, this.opts.rowEnd === 'next', this.outsideGroups());
    }

    currentGroup() {
        return this.map.isEmpty ? null : this.map.groupAt(this.row, this.col);
    }

    outsideGroups() {
        return (r, c) => this.map.groupAt(r, c) === null;
    }

    moveInGroup(order, back) {
        const next = nextInGroup(this.map, this.currentGroup(), this.row, this.col, order, back, this.opts.skipReadOnly !== 'none');
        if (next) {
            this.moveTo(next.r, next.c);
        }
    }

    // Returns false at the row end when set to leave the sheet, so the browser's default Tab moves focus out
    tabMove(back) {
        if (this.hasRange()) {
            this.moveInRange('row', back);
            return true;
        }

        const group = this.currentGroup();
        const next = this.tabTarget(back);
        if (!next) {
            return group !== null || this.opts.rowEnd !== 'leave';
        }

        const start = this.tabStartCol >= 0 ? this.tabStartCol : this.col;
        const sameRow = next.r === this.row;
        this.moveTo(next.r, next.c);
        if (!back && sameRow && this.opts.tabReturn && group === null) {
            this.tabStartCol = start;
        }
        return true;
    }

    moveInRange(order, back) {
        const next = nextInRange(this.map, this.selectionRange(), this.row, this.col, order, back, this.opts.skipReadOnly !== 'none');
        if (next) {
            this.moveTo(next.r, next.c, { keep: true });
        }
    }

    page(step) {
        if (this.map.isEmpty) {
            return;
        }

        this.moveTo(clamp(this.row + step * this.pageRows(), 0, this.map.rows - 1), this.col);
    }

    pageRows() {
        const height = (this.opts.autosize ? window.innerHeight : this.viewport.clientHeight - this.map.headerHeight) - this.frozenHeight;
        return Math.max(1, Math.floor(height / this.map.defaultRowHeight) - 1);
    }

    moveTo(row, col, { scroll = true, keep = false } = {}) {
        this.closePopup();
        this.closeMenu();
        this.tip.hidden = true;
        if (this.map.isEmpty) {
            this.place();
            return;
        }

        this.tabStartCol = -1;
        const target = this.map.anchorOf(clamp(row, 0, this.map.rows - 1), clamp(col, 0, this.map.cols - 1));
        this.row = target.r;
        this.col = target.c;
        if (!keep) {
            this.collapse();
        }
        this.place();
        this.updateSink();
        if (scroll) {
            this.frame.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        }
        this.notifyActive();
    }

    // ---- Additional key operations ----

    enterToday() {
        if (this.canEdit(this.currentRule())) {
            this.startInput(formatDate(today(), this.map.reading));
        }
    }

    commitInPlace() {
        if (!this.hasRange()) {
            this.commit(null);
            return;
        }

        const rule = this.currentRule();
        const text = this.sink.value;
        const result = checkInput(rule, text, this.map.messages, this.map.reading);
        if (!result.ok && rule.policy === 'stay') {
            this.showError(result.error);
            return;
        }

        this.finishEdit();
        this.fillRange(text);
    }

    async fillWithActive() {
        if (!this.hasRange()) {
            return;
        }

        const text = await this.editText(this.row, this.col);
        if (!this.disposed && this.state === READY) {
            this.fillRange(text);
        }
    }

    fillRange(text) {
        const range = this.selectionRange();
        this.runRange('FillAsync', range.top, range.left, range.bottom, range.right, text);
    }

    fillFrom(method) {
        const range = this.selectionRange();
        this.runRange(method, range.top, range.left, range.bottom, range.right);
    }

    async runRange(method, ...args) {
        this.setState(COMMITTING);
        this.updateSink();
        let result = null;
        try {
            await this.lastSend;
            result = await this.dotnet.invokeMethodAsync(method, ...args);
        } catch {
            result = null;
        }

        if (this.disposed) {
            return;
        }

        this.setState(READY);
        this.updateSink();
        if (result?.message) {
            this.notice(result.message);
        }
        this.replay();
    }

    jumpHome(extend) {
        if (this.map.isEmpty) {
            return;
        }

        // With frozen panes, Ctrl+Home goes to the top-left cell of the unfrozen area, as in Excel
        const home = { r: Math.min(this.frozenRows, this.map.rows - 1), c: Math.min(this.frozenCols, this.map.cols - 1) };
        const first = this.opts.skipReadOnly === 'all' ? firstInputCell(this.map) : home;
        if (extend) {
            this.extendTo(first.r, first.c);
        } else {
            this.moveTo(first.r, first.c);
        }
    }

    async jump(direction, extend) {
        if (this.map.isEmpty) {
            return;
        }

        const from = extend ? this.extent : { r: this.row, c: this.col };
        this.setState(COMMITTING);
        this.updateSink();
        let target = null;
        try {
            await this.lastSend;
            target = await this.dotnet.invokeMethodAsync('FindEdge', from.r, from.c, direction);
        } catch {
            target = null;
        }

        if (this.disposed) {
            return;
        }

        this.setState(READY);
        this.updateSink();
        if (target && extend) {
            this.extendTo(target[0], target[1]);
        } else if (target) {
            this.moveTo(target[0], target[1]);
        }
        this.replay();
    }

    // ---- Range selection ----

    selectionRange() {
        return this.map.expandRange(rangeOf(this.anchor, this.extent));
    }

    hasRange() {
        if (this.anchor.r === this.extent.r && this.anchor.c === this.extent.c) {
            return false;
        }

        const range = this.selectionRange();
        const own = this.map.mergeAt(this.row, this.col) ?? { top: this.row, left: this.col, bottom: this.row, right: this.col };
        return range.top !== own.top || range.left !== own.left || range.bottom !== own.bottom || range.right !== own.right;
    }

    collapse() {
        this.anchor = { r: this.row, c: this.col };
        this.extent = { r: this.row, c: this.col };
    }

    clampCell(cell) {
        return { r: clamp(cell.r, 0, Math.max(this.map.rows - 1, 0)), c: clamp(cell.c, 0, Math.max(this.map.cols - 1, 0)) };
    }

    select(anchor, extent, active = anchor, { scroll = true } = {}) {
        this.moveTo(active.r, active.c, { scroll, keep: true });
        this.anchor = this.clampCell(anchor);
        this.extent = this.clampCell(extent);
        this.placeSelection();
        this.notifyActive();
    }

    selectAll() {
        if (this.map.isEmpty) {
            return;
        }

        this.select({ r: 0, c: 0 }, { r: this.map.rows - 1, c: this.map.cols - 1 }, { r: this.row, c: this.col }, { scroll: false });
    }

    extendByKey(key) {
        let { r, c } = this.extent;
        // From a merged corner, step from the merge's edge so the extent jumps over the whole merge
        const merge = this.map.mergeAt(r, c) ?? { top: r, left: c, bottom: r, right: c };
        switch (key) {
            case 'ArrowUp':
                r = merge.top - 1;
                break;
            case 'ArrowDown':
                r = merge.bottom + 1;
                break;
            case 'ArrowLeft':
                c = merge.left - 1;
                break;
            case 'ArrowRight':
                c = merge.right + 1;
                break;
            case 'Home':
                c = 0;
                break;
            case 'End':
                c = this.map.cols - 1;
                break;
            default:
                r += (key === 'PageUp' ? -1 : 1) * this.pageRows();
                break;
        }
        this.extendTo(r, c);
    }

    extendTo(row, col, { scroll = true } = {}) {
        if (this.map.isEmpty) {
            return;
        }

        this.extent = this.clampCell({ r: row, c: col });
        this.placeSelection();
        if (scroll) {
            this.scrollToCell(this.extent.r, this.extent.c);
        }
        this.notifyActive();
    }

    selectHeader(head, shift) {
        if (this.map.isEmpty) {
            return;
        }

        const last = { r: this.map.rows - 1, c: this.map.cols - 1 };
        const here = { r: this.row, c: this.col };
        if (head.kind === 'all') {
            this.selectAll();
        } else if (head.kind === 'column' && shift) {
            this.select({ r: 0, c: this.anchor.c }, { r: last.r, c: head.index }, here, { scroll: false });
        } else if (head.kind === 'column') {
            this.select({ r: 0, c: head.index }, { r: last.r, c: head.index }, { r: this.visibleTopLeft().r, c: head.index }, { scroll: false });
        } else if (shift) {
            this.select({ r: this.anchor.r, c: 0 }, { r: head.index, c: last.c }, here, { scroll: false });
        } else {
            this.select({ r: head.index, c: 0 }, { r: head.index, c: last.c }, { r: head.index, c: this.visibleTopLeft().c }, { scroll: false });
        }
    }

    visibleTopLeft() {
        if (this.opts.autosize) {
            return { r: 0, c: 0 };
        }

        return {
            r: this.frozenRows > 0 ? 0 : indexAt(this.map.rowTops, this.viewport.scrollTop),
            c: this.frozenCols > 0 ? 0 : indexAt(this.map.colLefts, this.viewport.scrollLeft),
        };
    }

    placeSelection() {
        const shown = new Set();
        if (!this.map.isEmpty && this.hasRange()) {
            for (const piece of selectionPieces(this.selectionRange(), this.frozenRows, this.frozenCols)) {
                this.drawSelection(this.selections[piece.pane], piece);
                shown.add(piece.pane);
            }
        }

        for (const [pane, element] of Object.entries(this.selections)) {
            element.hidden = !shown.has(pane);
        }
        this.placeHandle();
    }

    placeHandle() {
        const show = this.opts.allowFill && this.state === READY && !this.map.isEmpty;
        this.handle.hidden = !show;
        if (!show) {
            return;
        }

        const range = this.fillSource();
        moveInto(this.decorLayers[paneOf(range.bottom, range.right, this.frozenRows, this.frozenCols)], this.handle);
        const box = this.map.cellBox(range.bottom, range.right);
        const style = this.handle.style;
        style.left = `${box.x + box.w - 4}px`;
        style.top = `${box.y + box.h - 4}px`;
    }

    fillSource() {
        return this.hasRange()
            ? this.selectionRange()
            : this.map.mergeAt(this.row, this.col) ?? { top: this.row, left: this.col, bottom: this.row, right: this.col };
    }

    placeFillRange(range) {
        const shown = new Set();
        if (range) {
            for (const piece of selectionPieces(range, this.frozenRows, this.frozenCols)) {
                const element = this.fillRanges[piece.pane];
                this.drawSelection(element, piece);
                element.style.clipPath = '';
                shown.add(piece.pane);
            }
        }

        for (const [pane, element] of Object.entries(this.fillRanges)) {
            element.hidden = !shown.has(pane);
        }
    }

    drawSelection(element, piece) {
        const first = this.map.cellBox(piece.top, piece.left);
        const last = this.map.cellBox(piece.bottom, piece.right);
        const edges = piece.edges;
        const left = first.x - (edges.left ? 1 : 0);
        const top = first.y - (edges.top ? 1 : 0);
        const width = last.x + last.w - left;
        const height = last.y + last.h - top;
        const style = element.style;
        style.left = `${left}px`;
        style.top = `${top}px`;
        style.width = `${width}px`;
        style.height = `${height}px`;
        style.borderTopWidth = edges.top ? '' : '0';
        style.borderLeftWidth = edges.left ? '' : '0';
        style.borderBottomWidth = edges.bottom ? '' : '0';
        style.borderRightWidth = edges.right ? '' : '0';
        const inside = this.row >= piece.top && this.row <= piece.bottom && this.col >= piece.left && this.col <= piece.right;
        if (inside) {
            const active = this.map.cellRect(this.row, this.col);
            const x = active.x - left;
            const y = active.y - top;
            style.clipPath = `path(evenodd, "M0 0H${width}V${height}H0Z M${x} ${y}H${x + active.w}V${y + active.h}H${x}Z")`;
        } else {
            style.clipPath = '';
        }
    }

    // Scrolls an invisible probe positioned from the sheet map into view, so the cell itself need not be rendered
    scrollToCell(row, col) {
        const pane = paneOf(row, col, this.frozenRows, this.frozenCols);
        moveInto(this.decorLayers[pane], this.probe);
        const rect = this.map.cellRect(row, col);
        const style = this.probe.style;
        style.left = `${rect.x}px`;
        style.top = `${rect.y}px`;
        style.width = `${rect.w}px`;
        style.height = `${rect.h}px`;
        this.setScrollMargin(style, pane);
        this.probe.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    }

    // Cells outside the frozen rows (columns) must not hide under the headers or the frozen part.
    // Frozen cells are visible without scrolling in that direction, so they get no margin.
    setScrollMargin(style, pane) {
        const rows = pane === 'main' || pane === 'l';
        const cols = pane === 'main' || pane === 't';
        style.scrollMarginTop = rows ? `${this.map.headerHeight + this.frozenHeight}px` : '0px';
        style.scrollMarginLeft = cols ? `${this.map.rowHeaderWidth + this.frozenWidth}px` : '0px';
    }

    // The sink keeps focus when moved. It is not moved during input or edit, which would interrupt the IME
    // composition; it moves once editing ends.
    attachToPane() {
        const pane = paneOf(this.row, this.col, this.frozenRows, this.frozenCols);
        this.pane = pane;
        moveInto(this.decorLayers[pane], this.frame);
        moveInto(this.decorLayers[pane], this.drop);
        moveInto(this.inputLayers[pane], this.tip);
        moveInto(this.inputLayers[pane], this.note);
        if (!this.isEditing()) {
            moveInto(this.inputLayers[pane], this.sink);
        }
        return pane;
    }

    get frozenRows() {
        return Math.min(this.opts.frozenRows ?? 0, this.map.rows);
    }

    get frozenCols() {
        return Math.min(this.opts.frozenColumns ?? 0, this.map.cols);
    }

    get frozenHeight() {
        return this.map.rowTops[this.frozenRows];
    }

    get frozenWidth() {
        return this.map.colLefts[this.frozenCols];
    }

    // Positions come from the sheet map, not the DOM, so rows that are not rendered still work
    place() {
        if (this.map.isEmpty) {
            this.frame.hidden = true;
            this.drop.hidden = true;
            for (const piece of Object.values(this.selections)) {
                piece.hidden = true;
            }
            this.handle.hidden = true;
            this.syncNote();
            return;
        }

        const pane = this.attachToPane();
        this.frame.hidden = false;
        const rect = this.map.cellRect(this.row, this.col);
        // With no headers, the frame's outer line at row 1 / column A would be cut off by the sheet edge,
        // so the frame is moved inside the cell.
        const box = this.map.mergeAt(this.row, this.col) ?? { top: this.row, left: this.col };
        const edgeTop = box.top === 0 && this.map.headerHeight === 0;
        const edgeLeft = box.left === 0 && this.map.rowHeaderWidth === 0;
        this.sink.classList.toggle('mussel-sheet-edge-t', edgeTop);
        this.sink.classList.toggle('mussel-sheet-edge-l', edgeLeft);
        const frame = this.frame.style;
        frame.left = `${rect.x - (edgeLeft ? 0 : 1)}px`;
        frame.top = `${rect.y - (edgeTop ? 0 : 1)}px`;
        frame.width = `${rect.w + (edgeLeft ? 0 : 1)}px`;
        frame.height = `${rect.h + (edgeTop ? 0 : 1)}px`;
        this.setScrollMargin(frame, pane);
        const sink = this.sink.style;
        sink.left = `${rect.x}px`;
        sink.top = `${rect.y}px`;
        sink.width = `${rect.w - 1}px`;
        sink.height = `${rect.h - 1}px`;
        const multiline = !!this.currentRule().multiline;
        this.sink.classList.toggle('mussel-sheet-ml', multiline);
        sink.lineHeight = multiline ? '' : `${rect.h - 1}px`;
        this.cellWidth = rect.w - 1;
        this.cellHeight = rect.h - 1;
        this.fit();
        this.placeDrop(rect);
        this.placeSelection();
        if (!this.tip.hidden) {
            this.placeTip();
        }
        this.syncNote();
        this.placeFloatings();
    }

    placeDrop(rect) {
        const rule = this.currentRule();
        const show = this.state !== CUSTOM && !rule.disabled
            && (rule.kind === 'list' || rule.kind === 'date' || this.canOpenCustom(rule));
        this.drop.hidden = !show;
        if (show) {
            const style = this.drop.style;
            style.left = `${rect.x + rect.w}px`;
            style.top = `${rect.y}px`;
            style.height = `${rect.h - 1}px`;
            style.lineHeight = `${rect.h - 1}px`;
        }
    }

    updateSink() {
        if (this.map.isEmpty) {
            this.sink.readOnly = true;
            return;
        }

        const rule = this.currentRule();
        this.sink.readOnly = this.state === COMMITTING || !this.canEdit(rule);
        this.applyHint(rule);
    }

    applyHint(rule) {
        const mode = rule.ime === 'half' && this.canEdit(rule) ? HALFWIDTH_INPUT_MODE : null;
        if (this.sink.getAttribute('inputmode') === mode) {
            return;
        }

        if (mode) {
            this.sink.setAttribute('inputmode', mode);
        } else {
            this.sink.removeAttribute('inputmode');
        }

        // Some browsers ignore an inputmode change while the element is focused, so re-focus it while ready
        if (this.state === READY && document.activeElement === this.sink) {
            this.sink.blur();
            this.focus();
        }
    }

    notifyActive() {
        clearTimeout(this.activeTimer);
        this.activeTimer = setTimeout(() => {
            const key = this.selectionKey();
            if (this.notified === key) {
                return;
            }
            this.notified = key;
            const range = this.selectionRange();
            this.send('OnActiveCellChangedAsync', this.row, this.col, range.top, range.left, range.bottom, range.right);
        }, ACTIVE_NOTIFY_DELAY_MS);
    }

    // ---- Editing ----

    startInput(text) {
        this.setState(INPUT);
        this.sink.value = text;
        this.showEditor();
        this.sink.setSelectionRange(text.length, text.length);
    }

    async startEdit() {
        const row = this.row;
        const col = this.col;
        const text = await this.editText(row, col);
        if (this.disposed || row !== this.row || col !== this.col || this.state !== READY) {
            return;
        }

        this.setState(EDIT);
        this.sink.value = text;
        this.showEditor();
        this.sink.setSelectionRange(text.length, text.length);
        if (this.currentRule().kind === 'date') {
            this.openCalendar(false);
        }
    }

    editText(row, col) {
        const cell = this.cellElement(row, col);
        if (cell) {
            return Promise.resolve(cell.dataset.edit ?? cell.textContent);
        }

        return this.dotnet.invokeMethodAsync('GetEditText', row, col).catch(() => '');
    }

    // Quick-validate in JS, then move first and send to C# without waiting (optimistic).
    // When configured to wait for C#, move only after its result.
    // forceOptimistic is for leaving the sheet.
    async commit(moveAction, forceOptimistic = false) {
        const row = this.row;
        const col = this.col;
        const text = this.sink.value;
        const previous = this.state;
        const rule = this.map.rule(row, col);
        const result = checkInput(rule, text, this.map.messages, this.map.reading);
        if (!result.ok && rule.policy === 'stay' && !forceOptimistic) {
            this.showError(result.error);
            return;
        }

        if (!result.ok && rule.policy === 'revert') {
            this.finishEdit();
            moveAction?.();
            return;
        }

        if (forceOptimistic || this.opts.commitMode === 'optimistic' || !result.ok) {
            // Send the commit before moving, so a move that adds a row is recorded after it
            this.finishEdit();
            this.send('CommitAsync', row, col, text, true);
            moveAction?.();
            return;
        }

        this.closePopup();
        this.setState(COMMITTING);
        this.updateSink();
        let response;
        try {
            response = await this.dotnet.invokeMethodAsync('CommitAsync', row, col, text, false);
        } catch {
            response = { ok: false, error: this.map.messages.canceled, stored: false };
        }

        if (this.disposed) {
            return;
        }

        if (response.ok || response.stored || rule.policy === 'revert') {
            this.finishEdit();
            moveAction?.();
            this.replay();
            return;
        }

        this.queue = [];
        this.setState(previous);
        this.updateSink();
        this.showError(response.error);
    }

    replay() {
        const queued = this.queue;
        this.queue = [];
        for (const k of queued) {
            if (this.state === COMMITTING) {
                this.queue.push(k);
                continue;
            }
            this.handleKey({ ...k, replay: true });
        }
    }

    async history(redo) {
        const before = this.selectionKey();
        let target = null;
        try {
            target = await this.dotnet.invokeMethodAsync('UndoFromScriptAsync', redo);
        } catch {
        }

        // If the user selected another cell while waiting, keep that selection instead
        if (target && !this.disposed && this.state === READY && this.selectionKey() === before) {
            this.select({ r: target[0], c: target[1] }, { r: target[2], c: target[3] });
        }
    }

    selectionKey() {
        const range = this.selectionRange();
        return [this.row, this.col, range.top, range.left, range.bottom, range.right].join(',');
    }

    finishEdit() {
        this.closePopup();
        this.applyFont(null);
        this.sink.value = '';
        this.sink.placeholder = '';
        this.sink.classList.remove('mussel-sheet-editing', 'mussel-sheet-invalid');
        this.sink.removeAttribute('title');
        this.tip.hidden = true;
        this.setState(READY);
        this.updateSink();
        this.place();
    }

    showEditor() {
        const rule = this.currentRule();
        this.sink.placeholder = rule.mask ? maskDisplay(rule.mask) : '';
        this.applyFont(rule.font);
        this.sink.classList.add('mussel-sheet-editing');
        this.fit();
    }

    // null clears it: the ready sink is transparent, and a leftover inline color would make it visible
    applyFont(font) {
        const style = this.sink.style;
        style.fontWeight = font?.bold ? '600' : '';
        style.fontStyle = font?.italic ? 'italic' : '';
        style.textDecorationLine = [font?.underline ? 'underline' : '', font?.strikethrough ? 'line-through' : ''].filter(Boolean).join(' ');
        style.fontSize = font?.size ? `${font.size}px` : '';
        style.color = font?.color ?? '';
    }

    fit() {
        this.sink.style.width = `${this.cellWidth}px`;
        this.sink.style.height = `${this.cellHeight}px`;
        if (!this.sink.classList.contains('mussel-sheet-editing')) {
            return;
        }

        if (this.sink.classList.contains('mussel-sheet-ml')) {
            const lines = this.sink.scrollHeight;
            if (lines > this.cellHeight) {
                this.sink.style.height = `${lines}px`;
            }
            this.placeCaps();
            return;
        }

        if (this.sink.scrollWidth > this.cellWidth) {
            this.sink.style.width = `${this.sink.scrollWidth + 2}px`;
        }
        this.placeCaps();
    }

    showError(message) {
        this.closePopup();
        this.sink.classList.add('mussel-sheet-invalid');
        this.sink.title = message;
        this.tip.textContent = message;
        this.tip.hidden = false;
        this.placeTip();
        this.syncNote();
        this.placeCaps();
    }

    placeTip() {
        const rect = this.map.cellRect(this.row, this.col);
        this.tip.style.left = `${rect.x}px`;
        this.tip.style.top = `${rect.y + rect.h + 2}px`;
    }

    hideTip() {
        if (!this.tip.hidden) {
            this.tip.hidden = true;
            this.syncNote();
        }
    }

    syncNote() {
        const text = this.map.isEmpty ? null : this.currentRule().note;
        const show = !!text && this.tip.hidden && !this.popupKind && !this.menuOpen
            && this.state !== CUSTOM && document.activeElement === this.sink;
        if (show) {
            this.note.textContent = text;
            this.note.hidden = false;
            this.placeNote();
            this.sink.setAttribute('aria-describedby', this.note.id);
        } else if (!this.note.hidden) {
            this.note.hidden = true;
            this.sink.removeAttribute('aria-describedby');
        }
    }

    placeNote() {
        const rect = this.map.cellRect(this.row, this.col);
        const height = this.note.offsetHeight;
        const below = rect.y + rect.h + 2;
        const above = rect.y - height - 2;
        const v = this.viewport;
        const flip = !this.opts.autosize && below + height > v.scrollTop + v.clientHeight && above >= v.scrollTop + this.map.headerHeight;
        this.note.style.left = `${rect.x}px`;
        this.note.style.top = `${flip ? above : below}px`;
    }

    // ---- Lists, suggestions and calendar ----

    openDrop() {
        const rule = this.currentRule();
        if (rule.disabled) {
            return;
        }

        if (this.canOpenCustom(rule)) {
            this.openCustom();
            return;
        }

        if (rule.kind !== 'list' && rule.kind !== 'date' && rule.kind !== 'autocomplete') {
            return;
        }

        const open = () => {
            if (rule.kind === 'list') {
                this.openList(true);
            } else if (rule.kind === 'date') {
                this.openCalendar(true);
            } else {
                this.updateSuggestions(true);
            }
        };

        if (this.state === READY) {
            this.startEdit().then(() => {
                if (this.isEditing()) {
                    open();
                }
            });
        } else if (this.isEditing()) {
            open();
        }
    }

    openList(all) {
        const items = this.currentRule().items ?? [];
        const text = this.sink.value;
        const shown = all ? items.slice() : filterItems(items, text);
        if (!shown.length) {
            this.closePopup();
            return;
        }

        const current = shown.findIndex(x => sameText(x.text, text) || sameText(x.value, text));
        this.popupKind = 'list';
        this.popupItems = shown;
        this.popupIndex = all ? current : Math.max(current, 0);
        this.renderList();
        this.showPopup();
    }

    scheduleSuggest() {
        clearTimeout(this.suggestTimer);
        this.suggestTimer = setTimeout(() => this.updateSuggestions(false), SUGGEST_DELAY_MS);
    }

    async updateSuggestions(force) {
        clearTimeout(this.suggestTimer);
        const row = this.row;
        const col = this.col;
        const rule = this.map.rule(row, col);
        const text = this.sink.value;
        if (!this.isEditing() || rule.kind !== 'autocomplete') {
            return;
        }

        if (!force && !text.trim()) {
            this.closePopup();
            return;
        }

        let items;
        if (rule.suggest) {
            const seq = ++this.suggestSeq;
            let result = null;
            try {
                result = await this.dotnet.invokeMethodAsync('GetSuggestionsAsync', row, col, text);
            } catch {
                result = null;
            }

            if (seq !== this.suggestSeq || this.disposed || row !== this.row || col !== this.col || !this.isEditing() || this.sink.value !== text) {
                return;
            }
            items = (result ?? []).map(x => ({ value: x, text: x }));
        } else {
            items = filterItems(rule.items ?? [], text);
        }

        if (!items.length) {
            this.closePopup();
            return;
        }

        this.popupKind = 'suggest';
        this.popupItems = items;
        this.popupIndex = -1;
        this.renderList();
        this.showPopup();
    }

    renderList() {
        const list = createElement('div', 'mussel-sheet-list');
        list.setAttribute('role', 'listbox');
        this.popupItems.forEach((item, i) => {
            const element = createElement('div', i === this.popupIndex ? 'mussel-sheet-item mussel-sheet-focus' : 'mussel-sheet-item');
            element.setAttribute('role', 'option');
            element.textContent = item.text;
            element.dataset.i = String(i);
            list.append(element);
        });
        this.popup.replaceChildren(list);
    }

    highlight(index) {
        this.popupIndex = index;
        const items = this.popup.querySelectorAll('.mussel-sheet-item');
        items.forEach((element, i) => element.classList.toggle('mussel-sheet-focus', i === index));
        items[index]?.scrollIntoView({ block: 'nearest' });
    }

    openCalendar(nav) {
        const rule = this.currentRule();
        this.calSelected = parseDate(this.sink.value, this.map.reading);
        this.calendarNav = nav;
        this.calFocus = null;
        if (nav) {
            const start = this.calSelected ?? today();
            this.calFocus = inDateRange(start, rule) ? start : this.nearestInRange(start, rule);
        }

        const base = this.calFocus ?? this.calSelected ?? today();
        this.calMonth = { y: base.y, m: base.m };
        this.popupKind = 'calendar';
        this.renderCalendar();
        this.showPopup();
    }

    nearestInRange(date, rule) {
        if (rule.minimum && BOUND_DATE.test(rule.minimum) && dateKey(date) < rule.minimum) {
            return parseKey(rule.minimum);
        }

        return rule.maximum && BOUND_DATE.test(rule.maximum) ? parseKey(rule.maximum) : date;
    }

    followCalendar() {
        const date = parseDate(this.sink.value, this.map.reading);
        this.calSelected = date;
        if (date) {
            this.calMonth = { y: date.y, m: date.m };
            if (this.calendarNav) {
                this.calFocus = date;
            }
        }
        this.renderCalendar();
    }

    moveCalendarFocus(date) {
        this.calFocus = date;
        this.calMonth = { y: date.y, m: date.m };
        this.renderCalendar();
    }

    shiftMonth(step) {
        const next = addMonths({ ...this.calMonth, d: 1 }, step);
        this.calMonth = { y: next.y, m: next.m };
        if (this.calFocus) {
            this.calFocus = addMonths(this.calFocus, step);
        }
        this.renderCalendar();
    }

    renderCalendar() {
        const rule = this.currentRule();
        const { y, m } = this.calMonth;
        const calendar = createElement('div', 'mussel-sheet-cal');
        const head = createElement('div', 'mussel-sheet-cal-head');
        const title = createElement('span', 'mussel-sheet-cal-title');
        const info = this.map.calendar;
        const messages = this.map.messages;
        title.textContent = formatYearMonth(info.yearMonth, y, m, info.monthNames);
        head.append(createButton('mussel-sheet-cal-prev', '‹', messages.calendarPrevious), title, createButton('mussel-sheet-cal-next', '›', messages.calendarNext));

        const grid = createElement('div', 'mussel-sheet-cal-grid');
        for (let i = 0; i < 7; i++) {
            const weekday = (info.firstDay + i) % 7;
            const element = createElement('span', weekday === 0 ? 'mussel-sheet-cal-wd mussel-sheet-sun' : weekday === 6 ? 'mussel-sheet-cal-wd mussel-sheet-sat' : 'mussel-sheet-cal-wd');
            element.textContent = info.dayNames[weekday];
            grid.append(element);
        }

        const todayKey = dateKey(today());
        monthGrid(y, m, info.firstDay).forEach(date => {
            const day = createElement('span', 'mussel-sheet-cal-day');
            const key = dateKey(date);
            day.textContent = String(date.d);
            day.dataset.date = key;
            const classes = day.classList;
            classes.toggle('mussel-sheet-other', date.m !== m);
            classes.toggle('mussel-sheet-sun', dayOfWeek(date) === 0);
            classes.toggle('mussel-sheet-sat', dayOfWeek(date) === 6);
            classes.toggle('mussel-sheet-today', key === todayKey);
            classes.toggle('mussel-sheet-selected', this.calSelected !== null && sameDate(date, this.calSelected));
            classes.toggle('mussel-sheet-focus', this.calFocus !== null && sameDate(date, this.calFocus));
            classes.toggle('mussel-sheet-disabled', !inDateRange(date, rule));
            grid.append(day);
        });

        const foot = createElement('div', 'mussel-sheet-cal-foot');
        foot.append(createButton('mussel-sheet-cal-today', messages.calendarToday));
        calendar.append(head, grid, foot);
        this.popup.replaceChildren(calendar);
    }

    pickItem(index) {
        const item = this.popupItems[index];
        if (item && this.isEditing()) {
            this.sink.value = item.text;
            this.commit(null);
        }
    }

    pickDate(date) {
        if (this.isEditing() && inDateRange(date, this.currentRule())) {
            this.sink.value = formatDate(date, this.map.reading);
            this.commit(null);
        }
    }

    showPopup() {
        showFloating(this.popup);
        this.placePopup();
        this.syncNote();
    }

    closePopup() {
        clearTimeout(this.suggestTimer);
        this.suggestSeq++;
        if (!this.popupKind) {
            return;
        }

        this.popupKind = null;
        this.popupItems = [];
        this.popupIndex = -1;
        this.calendarNav = false;
        hideFloating(this.popup);
        this.popup.replaceChildren();
        this.syncNote();
    }

    placePopup() {
        if (!this.popupKind) {
            return;
        }

        const cell = this.cellClientRect();
        const style = this.popup.style;
        style.minWidth = `${Math.round(cell.width)}px`;
        const box = this.popup.getBoundingClientRect();
        let top = cell.bottom + 1;
        if (top + box.height > window.innerHeight && cell.top - box.height - 1 >= 0) {
            top = cell.top - box.height - 1;
        }
        style.left = `${Math.round(clamp(cell.left, 0, Math.max(0, window.innerWidth - box.width)))}px`;
        style.top = `${Math.round(top)}px`;
    }

    placeCustom() {
        if (!this.custom) {
            return;
        }

        const cell = this.cellClientRect();
        const style = this.custom.style;
        style.minWidth = `${Math.round(cell.width)}px`;
        const box = this.custom.getBoundingClientRect();
        style.left = `${Math.round(clamp(cell.left, 0, Math.max(0, window.innerWidth - box.width)))}px`;
        style.top = `${Math.round(clamp(cell.top, 0, Math.max(0, window.innerHeight - box.height)))}px`;
    }

    placeFloatings() {
        this.placePopup();
        this.placeCustom();
        this.placeCaps();
    }

    // Draws the active frame of row 1 / column A over the header lines too, as Excel does.
    // Headers sit above the frame and hide its outer 1px, so that line goes in a layer above them.
    // Not needed for frozen cells (already above) or while the cell is scrolled under a header.
    placeCaps() {
        this.capTop.hidden = true;
        this.capLeft.hidden = true;
        if (this.map.isEmpty || this.frame.hidden) {
            return;
        }

        const box = this.map.mergeAt(this.row, this.col) ?? { top: this.row, left: this.col };
        const showTop = box.top === 0 && this.frozenRows === 0 && this.map.headerHeight > 0;
        const showLeft = box.left === 0 && this.frozenCols === 0 && this.map.rowHeaderWidth > 0;
        if (!showTop && !showLeft) {
            return;
        }

        const ring = this.ringBox();
        const shift = this.stickyShift();
        const invalid = this.isEditing() && this.sink.classList.contains('mussel-sheet-invalid');
        if (showTop && shift.top < 0.5) {
            // A frozen-column cell does not scroll sideways, so it goes in the container that sticks both ways.
            // Others use the sideways-scrolling one and skip the part hidden under the row header and frozen columns.
            const fixed = this.pane === 'l';
            const hidden = shift.left + this.map.rowHeaderWidth + (fixed ? 0 : this.frozenWidth);
            const start = fixed || ring.x >= hidden - 1 ? ring.x : hidden;
            const end = ring.x + ring.w;
            if (end > start) {
                moveInto(fixed ? this.inputLayers.tl : this.inputLayers.t, this.capTop);
                this.showCap(this.capTop, start, this.map.headerHeight - 1, end - start, 1, invalid);
            }
        }

        if (showLeft && shift.left < 0.5) {
            moveInto(this.pane === 't' ? this.decorLayers.tl : this.decorLayers.l, this.capLeft);
            this.showCap(this.capLeft, this.map.rowHeaderWidth - 1, ring.y, 1, ring.h, invalid);
        }
    }

    showCap(cap, x, y, w, h, invalid) {
        const style = cap.style;
        style.left = `${x}px`;
        style.top = `${y}px`;
        style.width = `${w}px`;
        style.height = `${h}px`;
        cap.classList.toggle('mussel-sheet-invalid', invalid);
        cap.hidden = false;
    }

    ringBox() {
        if (this.isEditing()) {
            const sink = this.sink;
            return { x: sink.offsetLeft - 1, y: sink.offsetTop - 1, w: sink.offsetWidth + 2, h: sink.offsetHeight + 2 };
        }

        const frame = this.frame.style;
        return { x: parseFloat(frame.left), y: parseFloat(frame.top), w: parseFloat(frame.width), h: parseFloat(frame.height) };
    }

    stickyShift() {
        const content = this.content.getBoundingClientRect();
        const head = this.root.querySelector('.mussel-sheet-corner, .mussel-sheet-rowhead');
        return {
            top: this.top.getBoundingClientRect().top - content.top,
            left: head ? head.getBoundingClientRect().left - content.left : 0,
        };
    }

    cellClientRect() {
        const r = this.frame.getBoundingClientRect();
        return { left: r.left + 1, top: r.top + 1, right: r.right - 1, bottom: r.bottom - 1, width: r.width - 2, height: r.height - 2 };
    }

    onPopupMouseDown(e) {
        e.preventDefault();
        if (e.button !== 0) {
            return;
        }

        const item = e.target.closest('.mussel-sheet-item');
        if (item) {
            this.pickItem(Number(item.dataset.i));
            return;
        }

        const day = e.target.closest('.mussel-sheet-cal-day');
        if (day) {
            this.pickDate(parseKey(day.dataset.date));
        } else if (e.target.closest('.mussel-sheet-cal-prev')) {
            this.shiftMonth(-1);
        } else if (e.target.closest('.mussel-sheet-cal-next')) {
            this.shiftMonth(1);
        } else if (e.target.closest('.mussel-sheet-cal-today')) {
            this.pickDate(today());
        }
    }

    onPopupMouseOver(e) {
        const item = e.target.closest('.mussel-sheet-item');
        if (item) {
            this.highlight(Number(item.dataset.i));
        }
    }

    // ---- Custom editor ----

    canOpenCustom(rule) {
        return rule.kind === 'custom' && !rule.disabled && this.opts.customEditor;
    }

    async openCustom() {
        const rule = this.currentRule();
        if (this.state !== READY || !this.canOpenCustom(rule)) {
            return;
        }

        this.setState(CUSTOM);
        this.drop.hidden = true;
        let opened = false;
        try {
            opened = await this.dotnet.invokeMethodAsync('OpenCustomEditor', this.row, this.col);
        } catch {
            opened = false;
        }

        if (!opened && this.state === CUSTOM && !this.custom) {
            this.setState(READY);
            this.updateSink();
            this.place();
        }
    }

    showCustom(element) {
        if (this.disposed || !element) {
            return;
        }

        this.detachCustom();
        this.setState(CUSTOM);
        this.custom = element;
        this.customHandlers = {
            keydown: e => this.onCustomKeyDown(e),
            focusout: e => this.onCustomFocusOut(e),
        };
        for (const [type, handler] of Object.entries(this.customHandlers)) {
            element.addEventListener(type, handler);
        }

        this.drop.hidden = true;
        showFloating(element);
        this.placeCustom();
        const target = element.querySelector('[autofocus]') ?? element.querySelector(FOCUSABLE) ?? element;
        target.focus({ preventScroll: true });
    }

    // Keys the template already handled and Enter on a button or link are left to the editor
    onCustomKeyDown(e) {
        if (e.isComposing || e.keyCode === 229 || e.defaultPrevented) {
            return;
        }

        const back = e.shiftKey;
        switch (e.key) {
            case 'Escape':
                e.preventDefault();
                this.cancelCustom();
                break;
            case 'Enter':
                if (e.altKey || e.ctrlKey || e.target.closest?.('button, a, [role="button"]')) {
                    return;
                }
                e.preventDefault();
                this.commitCustom(() => this.enterMove(back));
                break;
            case 'Tab':
                e.preventDefault();
                this.commitCustom(() => this.tabMove(back));
                break;
            default:
                break;
        }
    }

    onCustomFocusOut(e) {
        if (this.state === CUSTOM && e.relatedTarget && !this.root.contains(e.relatedTarget)) {
            this.commitCustom(null);
        }
    }

    async commitCustom(moveAction) {
        if (this.state !== CUSTOM || this.customBusy) {
            return;
        }

        // Blur the focused input first so its change event reaches C# before the commit
        const active = document.activeElement;
        if (this.custom && active !== this.custom && this.custom.contains(active)) {
            active.blur();
        }

        this.customBusy = true;
        let result;
        try {
            result = await this.dotnet.invokeMethodAsync('CommitCustomEditorAsync');
        } catch {
            result = { closed: true, error: null };
        } finally {
            this.customBusy = false;
        }

        if (this.disposed || this.state !== CUSTOM) {
            return;
        }

        if (!result.closed) {
            this.custom?.classList.add('mussel-sheet-invalid');
            (this.custom?.querySelector(FOCUSABLE) ?? this.custom)?.focus({ preventScroll: true });
            return;
        }

        this.finishCustom();
        moveAction?.();
    }

    cancelCustom() {
        if (this.state !== CUSTOM) {
            return;
        }

        this.send('CancelCustomEditor');
        this.finishCustom();
    }

    finishCustom() {
        const element = this.custom;
        this.detachCustom();
        if (element) {
            element.classList.remove('mussel-sheet-invalid');
            hideFloating(element);
        }

        if (this.state === CUSTOM) {
            this.setState(READY);
        }
        this.updateSink();
        this.place();
        this.focus();
    }

    detachCustom() {
        if (this.custom && this.customHandlers) {
            for (const [type, handler] of Object.entries(this.customHandlers)) {
                this.custom.removeEventListener(type, handler);
            }
        }
        this.custom = null;
        this.customHandlers = null;
    }

    // ---- Context menu ----

    // Over the live editor, popups and the custom editor, the browser's own menu (text cut, paste) is kept
    onContextMenu(e) {
        if (this.menu.contains(e.target)) {
            e.preventDefault();
            return;
        }

        if (this.popup.contains(e.target) || this.custom?.contains(e.target) || this.map.isEmpty || this.state === CUSTOM) {
            return;
        }

        if (this.isEditing() && e.target === this.sink) {
            return;
        }

        const byKey = e.button !== 2;
        const head = byKey ? null : this.headerAt(e.target);
        const onActive = byKey || e.target === this.sink || e.target === this.drop;
        const cell = onActive ? { r: this.row, c: this.col } : this.cellOf(e.target);
        if (!head && !cell) {
            return;
        }

        e.preventDefault();
        if (byKey && this.menuOpen) {
            // The browser follows a keyboard-opened menu with a contextmenu event of its own; ignore it
            return;
        }

        const open = () => {
            const kind = this.selectForMenu(head, cell);
            if (byKey) {
                this.openMenuAtCell(kind);
            } else {
                this.openMenu(kind, e.clientX, e.clientY);
            }
        };

        if (this.isEditing()) {
            this.commit(open);
        } else if (this.state === READY) {
            open();
        }
    }

    cellOf(target) {
        const cell = target.closest?.('.mussel-sheet-cell');
        return cell ? { r: Number(cell.closest('.mussel-sheet-row').dataset.r), c: Number(cell.dataset.c) } : null;
    }

    selectForMenu(head, cell) {
        const range = this.selectionRange();
        const size = { rows: this.map.rows, cols: this.map.cols };
        if (head?.kind === 'row' || head?.kind === 'column') {
            const inside = head.kind === 'row'
                ? range.left === 0 && range.right === size.cols - 1 && head.index >= range.top && head.index <= range.bottom
                : range.top === 0 && range.bottom === size.rows - 1 && head.index >= range.left && head.index <= range.right;
            if (!inside) {
                this.selectHeader(head, false);
            }
            return head.kind;
        }

        if (head?.kind === 'all') {
            this.selectAll();
            return 'cell';
        }

        if (cell.r < range.top || cell.r > range.bottom || cell.c < range.left || cell.c > range.right) {
            this.moveTo(cell.r, cell.c, { scroll: false });
        }
        return menuKind(this.selectionRange(), size);
    }

    openMenuAtCell(kind = menuKind(this.selectionRange(), { rows: this.map.rows, cols: this.map.cols })) {
        if (this.map.isEmpty) {
            return;
        }

        const cell = this.cellClientRect();
        this.openMenu(kind, cell.left, cell.bottom);
        this.highlightMenu(nextMenuIndex(this.menuItems, -1, 1));
    }

    openMenu(kind, x, y) {
        this.closePopup();
        this.tip.hidden = true;
        const range = this.selectionRange();
        this.menuItems = menuEntries(kind, range, { rows: this.map.rows, cols: this.map.cols }, this.opts, this.hasEditable(range), this.map.messages);
        this.menuIndex = -1;
        this.renderMenu();
        this.menuOpen = true;
        showFloating(this.menu);
        this.placeMenu(x, y);
        this.syncNote();
    }

    hasEditable(range) {
        for (let r = range.top; r <= range.bottom; r++) {
            for (let c = range.left; c <= range.right; c++) {
                if (!this.map.isDisabled(r, c)) {
                    return true;
                }
            }
        }
        return false;
    }

    renderMenu() {
        const elements = this.menuItems.map((entry, i) => {
            if (!entry) {
                const separator = createElement('div', 'mussel-sheet-menu-sep');
                separator.setAttribute('role', 'separator');
                return separator;
            }

            const item = createElement('div', 'mussel-sheet-menu-item');
            item.setAttribute('role', 'menuitem');
            item.dataset.i = String(i);
            item.dataset.action = entry.action;
            if (entry.name) {
                item.dataset.name = entry.name;
            }
            if (entry.disabled) {
                item.setAttribute('aria-disabled', 'true');
            }
            const label = createElement('span', 'mussel-sheet-menu-label');
            label.textContent = entry.label;
            item.append(label);
            if (entry.key) {
                const key = createElement('span', 'mussel-sheet-menu-key');
                key.textContent = entry.key;
                item.append(key);
            }
            return item;
        });
        this.menu.replaceChildren(...elements);
    }

    placeMenu(x, y) {
        const box = this.menu.getBoundingClientRect();
        const left = x + box.width > window.innerWidth ? x - box.width : x;
        const top = y + box.height > window.innerHeight ? y - box.height : y;
        this.menu.style.left = `${Math.round(clamp(left, 0, Math.max(0, window.innerWidth - box.width)))}px`;
        this.menu.style.top = `${Math.round(clamp(top, 0, Math.max(0, window.innerHeight - box.height)))}px`;
    }

    highlightMenu(index) {
        this.menuIndex = index;
        for (const item of this.menu.querySelectorAll('.mussel-sheet-menu-item')) {
            item.classList.toggle('mussel-sheet-focus', Number(item.dataset.i) === index);
        }
    }

    closeMenu() {
        if (!this.menuOpen) {
            return;
        }

        this.menuOpen = false;
        this.menuItems = [];
        this.menuIndex = -1;
        hideFloating(this.menu);
        this.menu.replaceChildren();
        this.syncNote();
    }

    menuKey(k) {
        switch (k.key) {
            case 'ArrowDown':
            case 'ArrowUp':
                this.highlightMenu(nextMenuIndex(this.menuItems, this.menuIndex, k.key === 'ArrowDown' ? 1 : -1));
                return true;
            case 'Home':
            case 'End':
                this.highlightMenu(nextMenuIndex(this.menuItems, -1, k.key === 'Home' ? 1 : -1));
                return true;
            case 'Enter':
            case ' ':
                if (this.menuIndex >= 0) {
                    this.runMenu(this.menuIndex);
                } else {
                    this.closeMenu();
                }
                return true;
            case 'Escape':
            case 'Tab':
                this.closeMenu();
                return true;
            case 'Shift':
            case 'Control':
            case 'Alt':
            case 'Meta':
            case 'ContextMenu':
                return true;
            case 'F10':
                return k.shift;
            default:
                this.closeMenu();
                return false;
        }
    }

    onMenuClick(e) {
        const item = e.target.closest('.mussel-sheet-menu-item');
        if (item) {
            this.runMenu(Number(item.dataset.i));
        }
    }

    onMenuMouseOver(e) {
        const item = e.target.closest('.mussel-sheet-menu-item');
        if (item && !item.hasAttribute('aria-disabled')) {
            this.highlightMenu(Number(item.dataset.i));
        }
    }

    onDocumentMouseDown(e) {
        if (this.menuOpen && !this.menu.contains(e.target)) {
            this.closeMenu();
        }
    }

    runMenu(index) {
        const entry = this.menuItems[index];
        if (!entry || entry.disabled || this.state !== READY) {
            return;
        }

        this.closeMenu();
        switch (entry.action) {
            case 'cut':
                this.menuCopy(true);
                break;
            case 'copy':
                this.menuCopy(false);
                break;
            case 'paste':
                this.menuPaste();
                break;
            case 'app': {
                const range = this.selectionRange();
                this.send('OnMenuItemAsync', entry.name, range.top, range.left, range.bottom, range.right, this.row, this.col);
                break;
            }
            default:
                this.changeStructure(entry.action);
                break;
        }
    }

    // Without the async clipboard (e.g. http on a host other than localhost),
    // execCommand raises the copy and cut events instead.
    menuCopy(cut) {
        if (!navigator.clipboard?.write && !navigator.clipboard?.writeText) {
            document.execCommand(cut ? 'cut' : 'copy');
            return;
        }

        const range = this.selectionRange();
        if (this.hasRange()) {
            const values = this.renderedValues(range);
            this.writeClipboard(values ? Promise.resolve({ text: toTsv(values), html: this.htmlOf(values, range) }) : this.rangeText(range), true);
        } else {
            this.writeClipboard(this.editText(this.row, this.col).then(text => ({ text })), false);
        }

        if (cut) {
            this.cutSelection();
        }
    }

    // Reading the clipboard needs permission: Chrome and Edge ask, Firefox and Safari show a Paste prompt.
    // If it cannot be read, the user is told to press Ctrl+V.
    async menuPaste() {
        let text = null;
        try {
            text = await navigator.clipboard.readText();
        } catch {
            text = null;
        }

        if (this.disposed || this.state !== READY) {
            return;
        }

        if (text === null) {
            this.notice(this.map.messages.menuPasteFailed);
            return;
        }

        this.pasteText(text);
    }

    // C# applies the change, sends the new sheet info, then returns the range to select;
    // keys queued meanwhile replay after that selection.
    async changeStructure(action) {
        const range = this.selectionRange();
        const row = this.row;
        const col = this.col;
        this.setState(COMMITTING);
        this.updateSink();
        let result = null;
        try {
            await this.lastSend;
            result = await this.dotnet.invokeMethodAsync('ChangeStructureAsync', action, range.top, range.left, range.bottom, range.right, row, col);
        } catch {
            result = null;
        }

        if (this.disposed) {
            return;
        }

        this.setState(READY);
        this.updateSink();
        if (result) {
            this.select({ r: result[0], c: result[1] }, { r: result[2], c: result[3] }, { r: result[4], c: result[5] });
        }
        this.replay();
    }

    // ---- Mouse ----

    onMouseDown(e) {
        if (this.popup.contains(e.target) || this.menu.contains(e.target) || this.custom?.contains(e.target)) {
            return;
        }

        // A right-click must not take focus from the sink; the menu is opened by the contextmenu event
        if (e.button === 2 && this.state !== CUSTOM && e.target !== this.viewport && e.target !== this.sink) {
            e.preventDefault();
            this.focus();
            return;
        }

        if (e.button !== 0 || e.target === this.viewport || e.target === this.sink) {
            // Scrollbars (target is the viewport) and the live editor (caret placement) are left to the browser
            return;
        }

        e.preventDefault();
        if (e.target === this.drop) {
            this.focus();
            this.onDropClick();
            return;
        }

        if (e.target === this.handle) {
            this.focus();
            if (this.state === READY) {
                this.startDrag('fill');
                this.drag.source = this.fillSource();
                this.drag.target = null;
            }
            return;
        }

        const cell = e.target.closest('.mussel-sheet-cell');
        if (this.state === CUSTOM) {
            if (cell) {
                const row = Number(cell.closest('.mussel-sheet-row').dataset.r);
                const col = Number(cell.dataset.c);
                this.commitCustom(() => this.moveTo(row, col));
            }
            return;
        }

        this.focus();
        const head = this.headerAt(e.target);
        if (head) {
            this.onHeaderDown(head, e.shiftKey);
            return;
        }

        if (!cell) {
            return;
        }

        const row = Number(cell.closest('.mussel-sheet-row').dataset.r);
        const col = Number(cell.dataset.c);
        const rule = this.map.rule(row, col);
        const toggle = !e.shiftKey && rule.kind === 'check' && !rule.disabled && e.target.closest('.mussel-sheet-cb, .mussel-sheet-ct') !== null;
        if (this.isEditing()) {
            if (row !== this.row || col !== this.col) {
                this.commit(() => {
                    this.moveTo(row, col);
                    if (toggle) {
                        this.send('ToggleAsync', row, col);
                    }
                });
            }
            return;
        }

        if (this.state !== READY) {
            return;
        }

        if (e.shiftKey) {
            this.extendTo(row, col, { scroll: false });
            return;
        }

        this.moveTo(row, col);
        if (toggle) {
            this.send('ToggleAsync', row, col);
            return;
        }
        this.startDrag('cells');
    }

    headerAt(target) {
        const column = target.closest('.mussel-sheet-colhead-cell');
        if (column) {
            return { kind: 'column', index: Number(column.dataset.c) };
        }

        const row = target.closest('.mussel-sheet-rowhead');
        if (row) {
            return { kind: 'row', index: Number(row.parentElement.dataset.r) };
        }

        return target.closest('.mussel-sheet-corner') ? { kind: 'all', index: 0 } : null;
    }

    onHeaderDown(head, shift) {
        if (this.isEditing()) {
            this.commit(() => this.selectHeader(head, shift));
            return;
        }

        if (this.state !== READY) {
            return;
        }

        this.selectHeader(head, shift);
        if (head.kind !== 'all') {
            this.startDrag(head.kind === 'column' ? 'columns' : 'rows');
        }
    }

    startDrag(mode) {
        this.endDrag();
        const move = e => this.onDragMove(e);
        const up = e => this.endDrag(e);
        window.addEventListener('mousemove', move);
        window.addEventListener('mouseup', up);
        this.drag = { mode, move, up, x: null, y: null, timer: setInterval(() => this.autoScroll(), AUTO_SCROLL_MS) };
    }

    onDragMove(e) {
        if (!this.drag) {
            return;
        }

        if (e.buttons === 0) {
            this.endDrag();
            return;
        }

        this.drag.x = e.clientX;
        this.drag.y = e.clientY;
        this.dragTo();
    }

    dragTo() {
        const { mode, x, y } = this.drag;
        if (x === null) {
            return;
        }

        const at = this.cellAt(x, y);
        if (mode === 'fill') {
            const target = fillTarget(this.drag.source, at);
            if (!sameRange(target, this.drag.target)) {
                this.drag.target = target;
                this.placeFillRange(target);
            }
            return;
        }

        const extent = mode === 'columns'
            ? { r: this.map.rows - 1, c: at.c }
            : mode === 'rows' ? { r: at.r, c: this.map.cols - 1 } : at;
        if (extent.r !== this.extent.r || extent.c !== this.extent.c) {
            this.extent = extent;
            this.placeSelection();
            this.notifyActive();
        }
    }

    autoScroll() {
        if (!this.drag || this.drag.x === null || this.opts.autosize) {
            return;
        }

        const box = this.viewport.getBoundingClientRect();
        const left = box.left + this.map.rowHeaderWidth + this.frozenWidth;
        const top = box.top + this.map.headerHeight + this.frozenHeight;
        const { x, y } = this.drag;
        const dx = x < left ? x - left : Math.max(0, x - box.right);
        const dy = y < top ? y - top : Math.max(0, y - box.bottom);
        if (dx === 0 && dy === 0) {
            return;
        }

        this.viewport.scrollBy(this.drag.mode === 'rows' ? 0 : clamp(dx, -40, 40), this.drag.mode === 'columns' ? 0 : clamp(dy, -40, 40));
        this.dragTo();
    }

    endDrag(e = null) {
        if (!this.drag) {
            return;
        }

        const { mode, source, target } = this.drag;
        clearInterval(this.drag.timer);
        window.removeEventListener('mousemove', this.drag.move);
        window.removeEventListener('mouseup', this.drag.up);
        this.drag = null;
        if (mode === 'fill') {
            this.placeFillRange(null);
            if (e && target) {
                this.runFill(source, target, e.ctrlKey || e.altKey);
            }
        }
    }

    cellAt(clientX, clientY) {
        const box = this.content.getBoundingClientRect();
        const top = this.top ? this.top.getBoundingClientRect().top : box.top;
        const left = this.inputLayers.l.getBoundingClientRect().left;
        return {
            r: frozenIndex(this.map.rowTops, this.frozenRows, clientY - top - this.map.headerHeight, clientY - box.top - this.map.headerHeight),
            c: frozenIndex(this.map.colLefts, this.frozenCols, clientX - left - this.map.rowHeaderWidth, clientX - box.left - this.map.rowHeaderWidth),
        };
    }

    onDropClick() {
        if (this.state === READY) {
            this.openDrop();
        } else if (this.isEditing()) {
            if (this.popupKind && (this.popupKind !== 'calendar' || this.calendarNav)) {
                this.closePopup();
            } else {
                this.openDrop();
            }
        }
    }

    onDoubleClick(e) {
        if (e.target === this.handle) {
            if (this.state === READY) {
                const source = this.fillSource();
                const active = { r: this.row, c: this.col };
                this.runAndSelect(() => this.dotnet.invokeMethodAsync('AutoFillDownAsync', source.top, source.left, source.bottom, source.right), active);
            }
            return;
        }

        const cell = e.target.closest?.('.mussel-sheet-cell');
        if (!cell || this.state !== READY) {
            return;
        }

        const row = Number(cell.closest('.mussel-sheet-row').dataset.r);
        const col = Number(cell.dataset.c);
        if (row !== this.row || col !== this.col) {
            return;
        }

        const rule = this.currentRule();
        if (this.canEdit(rule)) {
            this.startEdit();
        } else if (this.canOpenCustom(rule)) {
            this.openCustom();
        }
    }

    // ---- Row virtualization ----

    onScroll() {
        this.checkViewport(false);
        this.placeFloatings();
    }

    checkViewport(force) {
        const v = this.viewport;
        const c = this.covered;
        if (force || v.scrollTop < c.top || v.scrollTop + v.clientHeight > c.bottom || v.scrollLeft < c.left || v.scrollLeft + v.clientWidth > c.right) {
            this.notifyViewport();
        }
    }

    // Only one call in flight; if the position changed meanwhile, the latest one is sent after it finishes
    async notifyViewport() {
        if (this.disposed) {
            return;
        }

        if (this.viewBusy) {
            this.viewPending = true;
            return;
        }

        this.viewBusy = true;
        try {
            do {
                this.viewPending = false;
                const v = this.viewport;
                await this.dotnet.invokeMethodAsync('OnViewportChanged', Math.round(v.scrollTop), Math.round(v.clientHeight), Math.round(v.scrollLeft), Math.round(v.clientWidth));
            } while (this.viewPending && !this.disposed);
        } catch {
        } finally {
            this.viewBusy = false;
        }
    }

    // ---- Helpers ----

    setState(state) {
        this.state = state;
        this.layer.dataset.mode = state;
        this.syncNote();
        this.placeHandle();
    }

    isEditing() {
        return this.state === INPUT || this.state === EDIT;
    }

    currentRule() {
        return this.map.rule(this.row, this.col);
    }

    canEdit(rule) {
        return !rule.disabled && rule.kind !== 'check' && rule.kind !== 'custom';
    }

    cellElement(row, col) {
        return this.root.querySelector(`.mussel-sheet-row[data-r="${row}"] .mussel-sheet-cell[data-c="${col}"]`);
    }

    // Fire and forget; lastSend lets later calls wait for it.
    // Failures such as a dropped connection are ignored.
    send(method, ...args) {
        this.lastSend = this.dotnet.invokeMethodAsync(method, ...args).catch(() => { });
    }
}
