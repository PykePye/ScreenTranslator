using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using ScreenTranslator.Translation;

namespace ScreenTranslator.Writing;

public sealed class WritingHistoryStore
{
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenTranslator");

    private static readonly string ChatHistoryPath = Path.Combine(DataDirectory, "chat_history.md");
    private static readonly string MistakeHistoryPath = Path.Combine(DataDirectory, "mistake_history.md");
    private readonly object _sync = new();

    public WritingSaveResult Save(string original, WritingImprovementResult result)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(DataDirectory);
            EnsureFile(ChatHistoryPath, "# Chat History\n\n");
            EnsureFile(MistakeHistoryPath, "# Mistake History\n\n");

            var chatId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{RandomNumberGenerator.GetHexString(3).ToLowerInvariant()}";
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.AppendAllText(ChatHistoryPath, BuildChatEntry(chatId, timestamp, original, result), Encoding.UTF8);

            var reminders = BuildMistakeEntry(chatId, timestamp, result.Mistakes);
            return new WritingSaveResult(chatId, reminders);
        }
    }

    /// <summary>
    /// Reads the patterns already recorded, most frequent first, so the next prompt can name the
    /// mistakes this writer actually repeats.
    /// </summary>
    public List<RecurringPattern> GetTopPatterns(int max = 5)
    {
        lock (_sync)
        {
            if (!File.Exists(MistakeHistoryPath)) return [];

            var history = File.ReadAllText(MistakeHistoryPath, Encoding.UTF8);
            return Regex.Matches(history, "^- Pattern: (?<id>.+?)\\s*$", RegexOptions.Multiline)
                .Select(m => PatternCatalog.Normalize(m.Groups["id"].Value))
                .GroupBy(id => id, StringComparer.Ordinal)
                .Select(g => new RecurringPattern(g.Key, g.Count()))
                .Where(p => p.Count >= 2)
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.Pattern, StringComparer.Ordinal)
                .Take(max)
                .ToList();
        }
    }

    private List<string> BuildMistakeEntry(string chatId, string timestamp, IEnumerable<WritingMistake> mistakes)
    {
        var items = mistakes
            .Where(m => !string.IsNullOrWhiteSpace(m.Description))
            .ToList();
        if (items.Count == 0) return [];

        var existingHistory = File.ReadAllText(MistakeHistoryPath, Encoding.UTF8);
        var newOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var reminders = new List<string>();
        var entry = new StringBuilder();
        entry.AppendLine($"## {timestamp} · {chatId}");

        foreach (var mistake in items)
        {
            var pattern = PatternCatalog.Normalize(mistake.Pattern);
            var previousOccurrences = CountPattern(existingHistory, pattern);
            newOccurrences.TryGetValue(pattern, out var occurrencesInThisMessage);
            var totalOccurrences = previousOccurrences + occurrencesInThisMessage + 1;
            newOccurrences[pattern] = occurrencesInThisMessage + 1;

            entry.AppendLine($"- Group: {PatternCatalog.GroupName(PatternCatalog.GroupOf(pattern))}");
            entry.AppendLine($"- Pattern: {pattern}");
            entry.AppendLine($"- Description: {SingleLine(mistake.Description)}");
            entry.AppendLine($"- Original: {SingleLine(mistake.OriginalFragment)}");
            entry.AppendLine($"- Suggested correction: {SingleLine(mistake.SuggestedCorrection)}");
            entry.AppendLine($"- Learning tip: {SingleLine(mistake.LearningTip)}");
            entry.AppendLine();

            if (totalOccurrences >= 3 && occurrencesInThisMessage == 0)
            {
                var tip = string.IsNullOrWhiteSpace(mistake.LearningTip)
                    ? "Hãy dừng lại một nhịp để kiểm tra lỗi này trước khi gửi."
                    : mistake.LearningTip.Trim();
                reminders.Add($"Lỗi lặp lại ({totalOccurrences} lần): {mistake.Description}. {tip}");
            }
        }

        File.AppendAllText(MistakeHistoryPath, entry.ToString(), Encoding.UTF8);
        return reminders;
    }

    private static string BuildChatEntry(string chatId, string timestamp, string original, WritingImprovementResult result)
    {
        var entry = new StringBuilder();
        entry.AppendLine($"## {timestamp} · {chatId}");
        AppendBlock(entry, "Original", original);
        AppendBlock(entry, "Minimal fix", result.Minimal);
        AppendBlock(entry, "Natural", result.Casual);
        AppendBlock(entry, "Professional", result.Professional);
        entry.AppendLine();
        return entry.ToString();
    }

    private static void AppendBlock(StringBuilder entry, string title, string text)
    {
        entry.AppendLine($"### {title}");
        foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            entry.AppendLine($"    {line}");
        entry.AppendLine();
    }

    private static void EnsureFile(string path, string header)
    {
        if (!File.Exists(path)) File.WriteAllText(path, header, Encoding.UTF8);
    }

    /// <summary>
    /// Counts a pattern across the history. Every recorded id is normalized before comparing, so
    /// entries written by earlier versions under free-form names still count toward the threshold.
    /// </summary>
    private static int CountPattern(string history, string pattern) =>
        Regex.Matches(history, "^- Pattern: (?<id>.+?)\\s*$", RegexOptions.Multiline)
            .Count(m => string.Equals(PatternCatalog.Normalize(m.Groups["id"].Value), pattern, StringComparison.Ordinal));

    private static string SingleLine(string? text) => Regex.Replace(text ?? string.Empty, "[\\r\\n]+", " ").Trim();
}

public sealed record WritingSaveResult(string ChatId, IReadOnlyList<string> Reminders);

/// <summary>A pattern the writer has already produced more than once.</summary>
public sealed record RecurringPattern(string Pattern, int Count);
