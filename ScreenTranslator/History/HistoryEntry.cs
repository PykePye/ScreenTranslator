namespace ScreenTranslator.History;

public class HistoryEntry
{
    public int Id { get; set; }
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public byte[] ImageBlob { get; set; } = Array.Empty<byte>();
    public string TranslatedText { get; set; } = string.Empty;
    public int IsError { get; set; } // SQLite uses 0/1 for bool
}