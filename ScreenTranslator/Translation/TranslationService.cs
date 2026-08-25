using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenTranslator.Writing;

namespace ScreenTranslator.Translation;

public class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public TranslationService(string apiKey, string model)
    {
        _apiKey = apiKey?.Trim() ?? string.Empty;
        _model = model?.Trim() ?? "gemini-1.5-flash";
        _httpClient = new HttpClient();
    }

    public async Task<string> ListModelsAsync()
    {
        if (string.IsNullOrEmpty(_apiKey)) return "Lỗi: Chưa nhập API Key.";
        
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<JsonElement>(body);
                var models = data.GetProperty("models").EnumerateArray()
                                .Select(m => m.GetProperty("name").GetString()?.Replace("models/", ""))
                                .Where(n => n != null);
                return "Danh sách Model: " + string.Join(", ", models);
            }
            return $"Lỗi ListModels ({response.StatusCode}): {body}";
        }
        catch (Exception ex)
        {
            return $"Lỗi kết nối: {ex.Message}";
        }
    }

    public async Task<string> TestConnectionAsync()
    {
        if (string.IsNullOrEmpty(_apiKey)) return "Lỗi: Chưa nhập API Key.";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = "Hello" } } }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode) return "OK: Kết nối Gemini thành công!";
            return $"[DEBUG-V2.3] Lỗi ({response.StatusCode}): {responseBody}";
        }
        catch (Exception ex)
        {
            return $"[DEBUG-V2.3] Lỗi kết nối: {ex.Message}";
        }
    }

    /// <summary>
    /// Dịch một đầu việc tiếng Việt sang tiếng Anh, BÁM theo văn phong báo cáo của người dùng
    /// (few-shot từ các câu cũ) và giữ nguyên thuật ngữ trong glossary.
    /// Trả về đúng một câu/đoạn tiếng Anh, không kèm lời dẫn.
    /// </summary>
    public async Task<string> TranslateTextStyledAsync(
        string vietnameseText,
        IEnumerable<string> styleExamples,
        IEnumerable<string> glossaryTerms,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) throw new Exception("API Key is missing.");
        if (string.IsNullOrWhiteSpace(vietnameseText)) return string.Empty;

        var glossary = string.Join(", ", glossaryTerms.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct());
        var examplesBlock = string.Join("\n", styleExamples
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .Select(e => "- " + e.Trim()));

        var prompt = PromptLibrary.Render(PromptLibrary.ReportStyleTranslation, new Dictionary<string, string>
        {
            ["GLOSSARY"] = PromptLibrary.Section("GLOSSARY (keep these terms verbatim):", glossary),
            ["STYLE_EXAMPLES"] = PromptLibrary.Section("STYLE EXAMPLES (mimic this voice):", examplesBlock),
            ["VIETNAMESE_INPUT"] = vietnameseText.Trim()
        });

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Gemini API Error!\nStatus: {response.StatusCode}\nResponse: {errorContent}");
        }

        var responseData = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
        var text = responseData?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        return CleanResult(text);
    }

    /// <summary>Gỡ các ký tự thừa Gemini hay thêm (quote, bullet, label) khỏi câu trả về.</summary>
    private static string CleanResult(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var t = text.Trim();
        if (t.StartsWith("ENGLISH:", StringComparison.OrdinalIgnoreCase))
            t = t.Substring("ENGLISH:".Length).Trim();
        t = t.TrimStart('-', '*', '•', ' ').Trim();
        if (t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\""))
            t = t.Substring(1, t.Length - 2).Trim();
        return t;
    }

    public async Task<string> TranslateImageAsync(byte[] imageBytes, string targetLanguage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_apiKey)) throw new Exception("API Key is missing.");

        var base64Image = Convert.ToBase64String(imageBytes);
        var prompt = PromptLibrary.Render(PromptLibrary.ImageTranslation, new Dictionary<string, string>
        {
            ["TARGET_LANGUAGE"] = targetLanguage
        });

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new { inline_data = new { mime_type = "image/png", data = base64Image } }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"[DEBUG-V2.3] Gemini API Error!\nStatus: {response.StatusCode}\nResponse: {errorContent}");
        }

        var responseData = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
        var text = responseData?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        return text ?? string.Empty;
    }

    /// <summary>
    /// Improves a message and reports the mistakes behind the changes.
    /// The prompt carries the user profile, the closed pattern catalog, and the mistakes he already
    /// repeats, so the assistant coaches this specific writer rather than a generic one.
    /// </summary>
    public async Task<WritingImprovementResult> ImproveWritingAsync(
        string originalText,
        WritingAudience audience,
        IReadOnlyList<RecurringPattern> recurringPatterns,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) throw new Exception("API Key is missing.");
        if (string.IsNullOrWhiteSpace(originalText)) throw new ArgumentException("Text is required.", nameof(originalText));

        var prompt = PromptLibrary.Render(PromptLibrary.WritingAssistant, new Dictionary<string, string>
        {
            ["USER_PROFILE"] = PromptLibrary.Load(PromptLibrary.UserProfile),
            ["RECIPIENT_CONTEXT"] = $"They are writing {audience.PromptDescription}. {audience.Guidance}",
            ["RECURRING_MISTAKES"] = BuildRecurringBlock(recurringPatterns),
            ["PATTERN_CATALOG"] = PatternCatalog.BuildPromptBlock(),
            ["USER_TEXT"] = originalText.Trim()
        });

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new { responseMimeType = "application/json" }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Gemini API Error! Status: {response.StatusCode}. Response: {errorContent}");
        }

        var responseData = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
        var responseText = responseData?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        var result = JsonSerializer.Deserialize<WritingImprovementResult>(ExtractJson(responseText), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null || string.IsNullOrWhiteSpace(result.Casual) || string.IsNullOrWhiteSpace(result.Professional))
            throw new Exception("The writing assistant returned an incomplete response.");

        result.Mistakes ??= [];

        // The minimal-edit version is the one he learns from; fall back to the casual rewrite
        // rather than failing the whole response when the model skips it.
        if (string.IsNullOrWhiteSpace(result.Minimal)) result.Minimal = result.Casual;

        // The catalog owns both the id and its group, so a mistake keeps the same identity
        // across calls and stays countable in the history.
        foreach (var mistake in result.Mistakes)
        {
            mistake.Pattern = PatternCatalog.Normalize(mistake.Pattern);
            mistake.Group = PatternCatalog.GroupOf(mistake.Pattern);
        }

        return result;
    }

    /// <summary>Renders the repeat-offender block, or nothing when there is no history yet.</summary>
    private static string BuildRecurringBlock(IReadOnlyList<RecurringPattern> recurringPatterns)
    {
        if (recurringPatterns.Count == 0) return string.Empty;

        var lines = recurringPatterns.Select(p => $"- {p.Pattern} — {p.Count} times");
        return $"""
            MISTAKES HE HAS MADE BEFORE (most frequent first):
            {string.Join("\n", lines)}
            Scan for these first. If he repeats one of them, begin the Vietnamese "description" with
            "Lỗi lặp lại:" so he notices the pattern rather than just the fix.
            """;
    }

    private static string ExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new Exception("The writing assistant returned no response.");

        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            trimmed = firstNewLine >= 0 ? trimmed[(firstNewLine + 1)..] : trimmed;
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) trimmed = trimmed[..lastFence];
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end < start) throw new Exception("The writing assistant returned invalid JSON.");
        return trimmed[start..(end + 1)];
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public Candidate[]? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public Part[]? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}

/// <summary>Who the message is going to, which sets how far the tone may travel from the writer's own voice.</summary>
/// <param name="Id">Stable key persisted in settings and history.</param>
/// <param name="VietnameseLabel">Text shown in the picker.</param>
/// <param name="PromptDescription">Completes the sentence "They are writing ...".</param>
/// <param name="Guidance">Extra instruction appended after that sentence.</param>
public sealed record WritingAudience(string Id, string VietnameseLabel, string PromptDescription, string Guidance)
{
    public static readonly WritingAudience Colleague = new(
        "colleague",
        "Tin nhắn cho đồng nghiệp",
        "a chat message to a colleague they work with daily",
        "Keep it short and friendly. Contractions are fine. Do not formalise it.");

    public static readonly WritingAudience Manager = new(
        "manager",
        "Email cho quản lý",
        "a short work email to their manager",
        "Polite and clear. Still their own voice - correct, not ceremonial.");

    public static readonly WritingAudience SgReport = new(
        "sg_report",
        "Báo cáo gửi SG",
        "a status report to the SG counterparts",
        "Lead with the fact or the number. Keep sentences short so a non-native reader can scan it.");

    public static readonly IReadOnlyList<WritingAudience> All = [Colleague, Manager, SgReport];

    public static WritingAudience FromId(string? id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Colleague;
}

public sealed class WritingImprovementResult
{
    /// <summary>His own sentences with only the errors corrected.</summary>
    public string Minimal { get; set; } = string.Empty;
    public string Casual { get; set; } = string.Empty;
    public string Professional { get; set; } = string.Empty;
    public List<WritingMistake> Mistakes { get; set; } = [];
}

public sealed class WritingMistake
{
    public int Group { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OriginalFragment { get; set; } = string.Empty;
    public string SuggestedCorrection { get; set; } = string.Empty;
    public string LearningTip { get; set; } = string.Empty;
}
