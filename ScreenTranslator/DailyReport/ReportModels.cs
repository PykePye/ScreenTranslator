namespace ScreenTranslator.DailyReport;

/// <summary>Một đầu việc trong báo cáo của một ngày.</summary>
public class ReportEntry
{
    public int Id { get; set; }
    public string WorkDate { get; set; } = string.Empty; // yyyy-MM-dd
    public int No { get; set; }                           // số thứ tự việc trong ngày (1..n)
    public string ViText { get; set; } = string.Empty;    // bản tiếng Việt gốc (rỗng với việc cố định)
    public string EnText { get; set; } = string.Empty;    // bản tiếng Anh đã duyệt
    public int IsFixed { get; set; }                      // 1 = đầu việc cố định (standing task)
}

/// <summary>Câu tiếng Anh cũ dùng làm mẫu phong cách cho AI (few-shot).</summary>
public class StyleExample
{
    public int Id { get; set; }
    public string EnText { get; set; } = string.Empty;
}

/// <summary>Thuật ngữ giữ nguyên khi dịch.</summary>
public class GlossaryTerm
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
}

/// <summary>Mẫu việc hay lặp — chèn nhanh, người dùng có thể thêm sau.</summary>
public class TaskTemplate
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ViText { get; set; } = string.Empty;
    public string EnText { get; set; } = string.Empty;
}
