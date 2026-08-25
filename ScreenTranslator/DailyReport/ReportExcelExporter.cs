using System.Globalization;
using System.IO;
using ClosedXML.Excel;

namespace ScreenTranslator.DailyReport;

/// <summary>
/// Xuất báo cáo 1 tuần ra file .xlsx theo lưới gốc:
/// dòng 1 = ngày, dòng 2 = thứ, cột A = No., cột B..G = Mon..Sat.
/// </summary>
public class ReportExcelExporter
{
    private readonly ReportRepository _repo;

    public ReportExcelExporter(ReportRepository repo) => _repo = repo;

    /// <summary>Xuất tuần chứa <paramref name="anyDateInWeek"/>. Trả về đường dẫn file đã lưu.</summary>
    public string ExportWeek(DateTime anyDateInWeek, string outputFolder, string reporterName)
    {
        var monday = WeekHelper.GetMonday(anyDateInWeek);
        var days = WeekHelper.WorkDates(monday); // Mon..Sat
        var saturday = days[^1];

        var entries = _repo.GetEntriesBetween(
            monday.ToString("yyyy-MM-dd"), saturday.ToString("yyyy-MM-dd"));

        // map: WorkDate -> (No -> EnText)
        var byDay = entries
            .GroupBy(e => e.WorkDate)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.No, x => x.EnText));

        int maxNo = entries.Count > 0 ? entries.Max(e => e.No) : 0;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(Sanitize(WeekHelper.WeekLabel(monday)));

        // Header row 1: No. + dates
        ws.Cell(1, 1).Value = "No.";
        for (int d = 0; d < days.Length; d++)
        {
            ws.Cell(1, 2 + d).Value = days[d];
            ws.Cell(1, 2 + d).Style.NumberFormat.Format = "dd/MM/yyyy";
        }

        // Header row 2: weekday names
        for (int d = 0; d < days.Length; d++)
            ws.Cell(2, 2 + d).Value = days[d].ToString("dddd", CultureInfo.InvariantCulture);

        // Body
        for (int n = 1; n <= maxNo; n++)
        {
            int row = 2 + n;
            ws.Cell(row, 1).Value = n;
            for (int d = 0; d < days.Length; d++)
            {
                var key = days[d].ToString("yyyy-MM-dd");
                if (byDay.TryGetValue(key, out var tasks) && tasks.TryGetValue(n, out var text))
                    ws.Cell(row, 2 + d).Value = text;
            }
        }

        int lastRow = Math.Max(2, 2 + maxNo);
        var full = ws.Range(1, 1, lastRow, 7);
        full.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        full.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        full.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        full.Style.Alignment.WrapText = true;

        var header = ws.Range(1, 1, 2, 7);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        ws.Column(1).Width = 6;
        for (int d = 0; d < days.Length; d++)
            ws.Column(2 + d).Width = 42;
        ws.SheetView.FreezeRows(2);

        Directory.CreateDirectory(outputFolder);
        var path = Path.Combine(outputFolder, WeekHelper.BuildFileName(reporterName, monday));
        wb.SaveAs(path);
        return path;
    }

    private static string Sanitize(string sheetName)
    {
        foreach (var ch in new[] { '\\', '/', '*', '?', ':', '[', ']' })
            sheetName = sheetName.Replace(ch, '-');
        return sheetName.Length > 31 ? sheetName[..31] : sheetName;
    }
}
