using System.IO;
using ClosedXML.Excel;

namespace ScreenTranslator.DailyReport;

/// <summary>
/// Import 1 lần file Excel báo cáo cũ → bóc các câu tiếng Anh làm kho mẫu phong cách.
/// Đọc toàn bộ sheet (kể cả sheet ẩn), bỏ qua ô tiêu đề/ngày/thứ.
/// </summary>
public static class StyleImporter
{
    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "No.", "No", "Monday", "Tuesday", "Wednesday", "Thursday",
        "Friday", "Saturday", "Sunday"
    };

    /// <summary>Trả về số câu mới được thêm vào kho mẫu.</summary>
    public static int ImportFromFile(string filePath, ReportRepository repo)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Không tìm thấy file Excel mẫu.", filePath);

        var sentences = new List<string>();

        using (var wb = new XLWorkbook(filePath))
        {
            foreach (var ws in wb.Worksheets)
            {
                var range = ws.RangeUsed();
                if (range == null) continue;

                foreach (var cell in range.CellsUsed())
                {
                    var text = cell.GetString().Trim();
                    if (IsTaskSentence(text))
                        sentences.Add(text);
                }
            }
        }

        return repo.AddStyleExamples(sentences);
    }

    /// <summary>Heuristic: là câu mô tả công việc, không phải tiêu đề/ngày/số.</summary>
    private static bool IsTaskSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (Skip.Contains(text)) return false;
        if (text.Length < 15) return false;                 // câu việc đủ dài
        if (!text.Any(char.IsLetter)) return false;         // loại ô toàn số/ngày
        if (!text.Contains(' ')) return false;              // câu có nhiều từ
        return true;
    }
}
