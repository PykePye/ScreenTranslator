using System.IO;
using Microsoft.Data.Sqlite;
using Dapper;

namespace ScreenTranslator.DailyReport;

/// <summary>
/// Lưu trữ Daily Report bằng SQLite (cùng thư mục với history.db của app).
/// Bảng: report_entry, style_example, glossary, template.
/// </summary>
public class ReportRepository
{
    private static readonly string DbDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenTranslator");

    private static readonly string DbPath = Path.Combine(DbDir, "report.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";

    public ReportRepository()
    {
        if (!Directory.Exists(DbDir)) Directory.CreateDirectory(DbDir);

        using var c = Open();
        c.Execute(@"CREATE TABLE IF NOT EXISTS report_entry (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            WorkDate TEXT NOT NULL,
            No INTEGER NOT NULL,
            ViText TEXT,
            EnText TEXT,
            IsFixed INTEGER DEFAULT 0);");
        c.Execute(@"CREATE TABLE IF NOT EXISTS style_example (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            EnText TEXT NOT NULL UNIQUE);");
        c.Execute(@"CREATE TABLE IF NOT EXISTS glossary (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Term TEXT NOT NULL UNIQUE);");
        c.Execute(@"CREATE TABLE IF NOT EXISTS template (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT NOT NULL,
            ViText TEXT,
            EnText TEXT);");

        EnsureSeed(c);
    }

    private static SqliteConnection Open()
    {
        var c = new SqliteConnection(ConnectionString);
        c.Open();
        return c;
    }

    /// <summary>Nạp glossary + template mặc định nếu DB còn trống (chỉ chạy 1 lần).</summary>
    private static void EnsureSeed(SqliteConnection c)
    {
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM glossary") == 0)
        {
            foreach (var term in ReportDefaults.Glossary)
                c.Execute("INSERT OR IGNORE INTO glossary (Term) VALUES (@term)", new { term });
        }

        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM template") == 0)
        {
            foreach (var t in ReportDefaults.Templates)
                c.Execute("INSERT INTO template (Title, ViText, EnText) VALUES (@Title, @ViText, @EnText)", t);
        }
    }

    // ---------- report_entry ----------

    public List<ReportEntry> GetEntries(string workDate)
    {
        using var c = Open();
        return c.Query<ReportEntry>(
            "SELECT * FROM report_entry WHERE WorkDate = @workDate ORDER BY No", new { workDate }).ToList();
    }

    public List<ReportEntry> GetEntriesBetween(string startDate, string endDate)
    {
        using var c = Open();
        return c.Query<ReportEntry>(
            "SELECT * FROM report_entry WHERE WorkDate >= @startDate AND WorkDate <= @endDate ORDER BY WorkDate, No",
            new { startDate, endDate }).ToList();
    }

    public int AddEntry(ReportEntry e)
    {
        using var c = Open();
        return c.ExecuteScalar<int>(
            @"INSERT INTO report_entry (WorkDate, No, ViText, EnText, IsFixed)
              VALUES (@WorkDate, @No, @ViText, @EnText, @IsFixed);
              SELECT last_insert_rowid();", e);
    }

    public void UpdateEntry(ReportEntry e)
    {
        using var c = Open();
        c.Execute("UPDATE report_entry SET ViText = @ViText, EnText = @EnText WHERE Id = @Id", e);
    }

    public void DeleteEntry(int id)
    {
        using var c = Open();
        c.Execute("DELETE FROM report_entry WHERE Id = @id", new { id });
    }

    /// <summary>Đánh lại số thứ tự cho liền mạch sau khi xoá.</summary>
    public void Renumber(string workDate)
    {
        using var c = Open();
        var rows = c.Query<ReportEntry>(
            "SELECT * FROM report_entry WHERE WorkDate = @workDate ORDER BY No, Id", new { workDate }).ToList();
        int no = 1;
        foreach (var r in rows)
            c.Execute("UPDATE report_entry SET No = @no WHERE Id = @Id", new { no = no++, r.Id });
    }

    public int NextNo(string workDate)
    {
        using var c = Open();
        return c.ExecuteScalar<int?>(
            "SELECT MAX(No) FROM report_entry WHERE WorkDate = @workDate", new { workDate }) is int max ? max + 1 : 1;
    }

    /// <summary>Tự điền 3 việc cố định cho ngày nếu ngày đó chưa có dữ liệu.</summary>
    public void SeedFixedTasks(string workDate)
    {
        using var c = Open();
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM report_entry WHERE WorkDate = @workDate", new { workDate }) > 0)
            return;

        int no = 1;
        foreach (var task in ReportDefaults.FixedTasks)
        {
            c.Execute(@"INSERT INTO report_entry (WorkDate, No, ViText, EnText, IsFixed)
                        VALUES (@workDate, @no, '', @task, 1)", new { workDate, no = no++, task });
        }
    }

    // ---------- style_example ----------

    public int StyleCount()
    {
        using var c = Open();
        return (int)c.ExecuteScalar<long>("SELECT COUNT(*) FROM style_example");
    }

    public int AddStyleExamples(IEnumerable<string> examples)
    {
        using var c = Open();
        int added = 0;
        foreach (var ex in examples)
        {
            if (string.IsNullOrWhiteSpace(ex)) continue;
            added += c.Execute("INSERT OR IGNORE INTO style_example (EnText) VALUES (@ex)", new { ex = ex.Trim() });
        }
        return added;
    }

    /// <summary>Lấy một mẫu phong cách đa dạng (ngẫu nhiên, giới hạn số lượng) để nhồi few-shot.</summary>
    public List<string> GetStyleSample(int max = 25)
    {
        using var c = Open();
        return c.Query<string>(
            "SELECT EnText FROM style_example ORDER BY RANDOM() LIMIT @max", new { max }).ToList();
    }

    // ---------- glossary ----------

    public List<string> GetGlossary()
    {
        using var c = Open();
        return c.Query<string>("SELECT Term FROM glossary ORDER BY Term").ToList();
    }

    // ---------- template ----------

    public List<TaskTemplate> GetTemplates()
    {
        using var c = Open();
        return c.Query<TaskTemplate>("SELECT * FROM template ORDER BY Id").ToList();
    }

    public int AddTemplate(TaskTemplate t)
    {
        using var c = Open();
        return c.ExecuteScalar<int>(
            @"INSERT INTO template (Title, ViText, EnText) VALUES (@Title, @ViText, @EnText);
              SELECT last_insert_rowid();", t);
    }
}
