using System.IO;
using Microsoft.Data.Sqlite;
using Dapper;

namespace ScreenTranslator.History;

public class HistoryRepository
{
    private static readonly string DbDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenTranslator");
    
    private static readonly string DbPath = Path.Combine(DbDir, "history.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";

    public HistoryRepository()
    {
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        if (!Directory.Exists(DbDir))
        {
            Directory.CreateDirectory(DbDir);
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        // Kiểm tra xem có phải bản cũ (snake_case) không
        bool isOldVersion = false;
        try
        {
            // Kiểm tra sự tồn tại của cột cũ
            connection.Execute("SELECT created_at FROM history LIMIT 1");
            isOldVersion = true;
        }
        catch
        {
            // Table chưa tồn tại hoặc đã là bản mới
        }

        if (isOldVersion)
        {
            connection.Execute("DROP TABLE history");
        }

        // Tạo table với chuẩn PascalCase
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS history (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedAt TEXT NOT NULL,
                ImageBlob BLOB,
                TranslatedText TEXT NOT NULL,
                IsError INTEGER DEFAULT 0
            )");
    }

    public void AddEntry(HistoryEntry entry)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Execute(
            "INSERT INTO history (CreatedAt, ImageBlob, TranslatedText, IsError) VALUES (@CreatedAt, @ImageBlob, @TranslatedText, @IsError)",
            entry);
    }

    public IEnumerable<HistoryEntry> GetLatest(int limit = 50)
    {
        try
        {
            using var connection = new SqliteConnection(ConnectionString);
            return connection.Query<HistoryEntry>(
                "SELECT * FROM history ORDER BY Id DESC LIMIT @limit",
                new { limit }).ToList();
        }
        catch
        {
            return Enumerable.Empty<HistoryEntry>();
        }
    }

    public void Cleanup(int keepLimit)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Execute(
            "DELETE FROM history WHERE Id NOT IN (SELECT Id FROM history ORDER BY Id DESC LIMIT @keepLimit)",
            new { keepLimit });
    }
}