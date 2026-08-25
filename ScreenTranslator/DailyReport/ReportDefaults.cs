namespace ScreenTranslator.DailyReport;

/// <summary>
/// Dữ liệu mặc định: 3 đầu việc cố định, glossary, template khởi tạo.
/// Template/glossary lưu trong DB nên người dùng có thể thêm sau này.
/// </summary>
public static class ReportDefaults
{
    /// <summary>Đường dẫn file Excel cũ để import kho mẫu phong cách (thử lần đầu).</summary>
    public const string DefaultStyleSourceFile =
        @"F:\New_folder\MES-Manufacturing Excution System\Manufacturing_Execution_System\Daily report - Phi.xlsx";

    /// <summary>3 đầu việc cố định, tự điền cho mỗi ngày (nguyên văn theo file đang dùng).</summary>
    public static readonly string[] FixedTasks =
    {
        "Maintenance and troubleshooting machine during production.",
        "Supervising RFID production: Check and solve the delay of Converse and Nike orders, make plan production for Brooks and Walmart delivery tomorrow.",
        "Check the data of Nike - Converse RFID after processed and upload to SG database."
    };

    /// <summary>Thuật ngữ giữ nguyên khi dịch.</summary>
    public static readonly string[] Glossary =
    {
        "RFID", "Komax", "Komax Thanh Hoa", "Nike", "Converse", "Brooks", "Walmart",
        "Decathlon", "SG database", "EPC", "TID", "ZSF-A6000", "converting machine",
        "cutting machine", "laminator", "liner winder", "color sensor", "white label",
        "hangtag", "polybag", "ATID", "C&H", "MES"
    };

    /// <summary>Mẫu việc hay lặp (có sẵn cả VN + EN để chèn nhanh, không cần gọi AI).</summary>
    public static readonly TaskTemplate[] Templates =
    {
        new() { Title = "Hỗ trợ Komax Thanh Hoá",  ViText = "Hỗ trợ Komax Thanh Hoá xử lý sự cố máy.",                 EnText = "Support Komax Thanh Hoa to troubleshoot machine problem during production." },
        new() { Title = "Kiểm tra & upload data",   ViText = "Kiểm tra data RFID sau xử lý và upload lên database SG.", EnText = "Check the RFID data after processed and upload to SG database." },
        new() { Title = "Chuẩn bị layout Brooks",   ViText = "Chuẩn bị layout label và hangtag cho Brooks.",          EnText = "Preparing layout for Brooks RFID label and hangtag." },
        new() { Title = "Đào tạo nhân viên",        ViText = "Đào tạo nhân viên vận hành máy.",                        EnText = "Training employee about machine operation." },
        new() { Title = "Nghỉ phép 1 ngày",         ViText = "Hôm nay nghỉ phép năm 1 ngày.",                          EnText = "Today I have annual leave 1 day." }
    };
}
