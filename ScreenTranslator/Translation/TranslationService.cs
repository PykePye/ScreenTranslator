using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    public async Task<string> TranslateImageAsync(byte[] imageBytes, string targetLanguage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_apiKey)) throw new Exception("API Key is missing.");

        var base64Image = Convert.ToBase64String(imageBytes);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = $"You are a translation tool. Look at the image, identify all visible text, translate it to {targetLanguage}. Output ONLY the translation, no preamble, no explanations." },
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