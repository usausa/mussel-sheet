namespace Example.Client.Components.Pages;

using System.Text.Json;

using Example.Client.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed partial class JapaneseFormPage
{
    private const decimal TaxRate = 0.1m;

    private const int FirstDetailRow = 8;

    private const int LastDetailRow = 12;

    private static readonly string[] ReportFiles = ["InvoiceData.cs", "InvoiceReportBuilder.cs"];

    private readonly SheetEventLog log = new();

    private readonly Sheet sheet = CreateSheet();

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    private static Sheet CreateSheet()
    {
        var sheet = new Sheet(18, 6) { Culture = CultureInfo.GetCultureInfo("ja-JP") };
        int[] widths = [90, 110, 110, 90, 110, 130];
        using (sheet.BeginUpdate())
        {
            for (var c = 0; c < widths.Length; c++)
            {
                sheet.Columns[c].Width = widths[c];
            }

            // 表題
            sheet.Rows[0].Height = 44;
            sheet.Range("A1:F1").Merge();
            sheet.Range("A1:F1").Configure(static c =>
            {
                c.Kind = CellKind.Label;
                c.Style.Bold = true;
                c.Style.FontSize = 20;
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });

            // 請求の情報と宛先
            foreach (var label in new[] { "A2", "D2", "A3", "D3", "A4", "D4", "A5", "A6", "A7" })
            {
                sheet.Range(label).Configure(Label);
            }

            foreach (var field in new[] { "B2:C2", "E2:F2", "B3:C3", "E3:F3", "B4:C4", "E4:F4", "B5:F5", "B6:F6", "B7:F7" })
            {
                sheet.Range(field).Merge();
            }

            sheet.Range("B2").Configure(static c =>
            {
                c.Kind = CellKind.Date;
                c.Format = "ggge年M月d日";
                c.InputMessage = "R8.11.5・令和8年11月5日 でも入力できます";
            });
            sheet.Range("E2").Configure(static c =>
            {
                c.Pattern = "[0-9]{4}-[0-9]{4}";
                c.ErrorMessage = "請求番号は 1234-5678 の形で入力してください";
                c.ImeMode = ImeMode.Off;
            });
            sheet.Range("B3").Configure(static c =>
            {
                c.Kind = CellKind.Date;
                c.InputMessage = "11/5・1105・11月5日 でも入力できます";
            });
            sheet.Range("E3").Configure(static c => c.MaxLength = 20);
            sheet.Range("B4").Configure(static c =>
            {
                c.Kind = CellKind.Mask;
                c.Mask = "999-9999";
                c.ImeMode = ImeMode.Off;
                c.InputMessage = "区切りは省けます";
            });
            sheet.Range("E4").Configure(static c =>
            {
                c.Pattern = "[0-9]{2,4}-[0-9]{2,4}-[0-9]{4}";
                c.ErrorMessage = "電話番号は 03-1234-5678 の形で入力してください";
                c.ImeMode = ImeMode.Off;
            });
            sheet.Range("B5").Configure(static c =>
            {
                c.CharacterConversions = CellCharacterConversions.HiraganaToKatakana | CellCharacterConversions.HalfwidthKatakanaToFullwidth;
                c.CharacterRestriction = CellCharacterRestriction.Katakana;
                c.InputMessage = "ひらがな・半角カナは全角カタカナに変換します";
            });
            sheet.Range("B6").Configure(static c => c.Required = true);

            // 明細: 品名は 3 列を結合する
            sheet.Range("A8:F8").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Center;
            });
            sheet.Range("A8:C8").Merge();
            for (var row = 9; row <= 13; row++)
            {
                sheet.Range($"A{row}:C{row}").Merge();
            }

            sheet.Range("D9:E13").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
                c.Minimum = 0;
                c.ImeMode = ImeMode.Off;
            });

            // 小計・消費税・合計
            sheet.Range("A14:E16").Configure(static c =>
            {
                Label(c);
                c.Style.HorizontalAlignment = HorizontalAlignment.Right;
            });
            for (var row = 14; row <= 16; row++)
            {
                sheet.Range($"A{row}:E{row}").Merge();
            }

            sheet.Range("F9:F16").Configure(static c =>
            {
                c.Kind = CellKind.Number;
                c.Format = "N0";
            });
            sheet.Range("F16").Configure(static c => c.Style.Bold = true);

            // 備考
            sheet.Range("A17:A18").Merge();
            sheet.Range("B17:F18").Merge();
            sheet.Range("A17").Configure(Label);
            sheet.Range("B17").Configure(static c =>
            {
                c.WrapText = true;
                c.Style.VerticalAlignment = VerticalAlignment.Top;
            });

            // 罫線
            sheet.Range("A2:F18").Configure(static c => c.Style.Borders = CellBorders.All(CellBorder.Thin));
            sheet.Range("A2:F7").BorderAround(CellBorder.Medium);
            sheet.Range("A8:F16").BorderAround(CellBorder.Medium);
            sheet.Range("A17:F18").BorderAround(CellBorder.Medium);
            sheet.Range("A8:F8").Configure(static c => c.Style.Borders.Bottom = CellBorder.DoubleLine);
            sheet.Range("A16:F16").Configure(static c => c.Style.Borders.Top = CellBorder.DoubleLine);

            sheet.SetValues(0, 0, [["請 求 書"]]);
            sheet["A2"].Value = "請求日";
            sheet["B2"].Value = new DateOnly(2026, 11, 5);
            sheet["D2"].Value = "請求番号";
            sheet["E2"].Value = "2026-0123";
            sheet["A3"].Value = "支払期限";
            sheet["B3"].Value = new DateOnly(2026, 11, 30);
            sheet["D3"].Value = "担当者";
            sheet["E3"].Value = "山田 太郎";
            sheet["A4"].Value = "郵便番号";
            sheet["B4"].Value = "100-0001";
            sheet["D4"].Value = "電話番号";
            sheet["E4"].Value = "03-1234-5678";
            sheet["A5"].Value = "フリガナ";
            sheet["B5"].Value = "ミホンショウジ カブシキガイシャ";
            sheet["A6"].Value = "宛先";
            sheet["B6"].Value = "見本商事 株式会社 御中";
            sheet["A7"].Value = "住所";
            sheet["B7"].Value = "東京都千代田区千代田 1-1";
            sheet.SetValues(7, 0, [["品名", null, null, "数量", "単価", "金額"]]);
            sheet.SetValues(8, 0, [["コピー用紙(A4)", null, null, 10m, 450m]]);
            sheet.SetValues(9, 0, [["ボールペン(黒)", null, null, 50m, 120m]]);
            sheet["A14"].Value = "小計";
            sheet["A15"].Value = "消費税(10%)";
            sheet["A16"].Value = "合計(税込)";
            sheet["A17"].Value = "備考";
            sheet["B17"].Value = "お振込手数料はご負担ください。\n納品書は別途お送りします。";

            // 計算するセルは自動で読み取り専用になる
            sheet.ComputeRows("F9:F13", ["D", "E"], static x => x.Get<decimal?>("D") * x.Get<decimal?>("E"));
            sheet.Compute("F14", ["F9:F13"], static x => x.Values("F9:F13").OfType<decimal>().Sum());
            sheet.Compute("F15", ["F14"], static x => Math.Floor(x.Get<decimal>("F14") * TaxRate));
            sheet.Compute("F16", ["F14", "F15"], static x => x.Get<decimal>("F14") + x.Get<decimal>("F15"));
        }

        return sheet;
    }

    // The server makes the PDF, and the browser saves it. The text is what the cells show, such as 令和8年11月5日
    private async Task CreatePdfAsync()
    {
        var invoice = new InvoiceData(
            new Dictionary<string, string?>
            {
                ["InvoiceDate"] = sheet["B2"].Text,
                ["InvoiceNo"] = sheet["E2"].Text,
                ["DueDate"] = sheet["B3"].Text,
                ["Contact"] = sheet["E3"].Text,
                ["PostalCode"] = sheet["B4"].Text,
                ["Phone"] = sheet["E4"].Text,
                ["Kana"] = sheet["B5"].Text,
                ["Customer"] = sheet["B6"].Text,
                ["Address"] = sheet["B7"].Text,
                ["Subtotal"] = sheet["F14"].Text,
                ["Tax"] = sheet["F15"].Text,
                ["Total"] = sheet["F16"].Text,
                ["Notes"] = sheet["B17"].Text,
            },
            [.. Enumerable.Range(FirstDetailRow, LastDetailRow - FirstDetailRow + 1).Select(DetailLine)]);

        var json = JsonSerializer.Serialize(invoice, InvoiceJsonContext.Default.InvoiceData);
        var error = await JS.InvokeAsync<string?>("exampleDownload", "api/reports/invoice", json, "請求書.pdf");
        log.Add(error is null ? "PDF を作成しました" : $"PDF を作成できませんでした: {error}");
    }

    private Dictionary<string, string?> DetailLine(int row) => new()
    {
        ["Item"] = sheet.GetText(row, 0),
        ["Quantity"] = sheet.GetText(row, 3),
        ["Price"] = sheet.GetText(row, 4),
        ["Amount"] = sheet.GetText(row, 5),
    };

    private static void Label(CellSettings c)
    {
        c.Kind = CellKind.Label;
        c.Style.Bold = true;
        c.Style.FillColor = "#eef2f7";
    }
}
