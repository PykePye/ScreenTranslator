using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ScreenTranslator.Translation;

/// <summary>
/// Loads prompt templates from the embedded Prompts folder and fills their {{PLACEHOLDER}} slots.
/// Prompts live in Markdown files rather than in C# string literals so their wording can be tuned
/// without touching code.
/// </summary>
public static class PromptLibrary
{
    public const string ImageTranslation = "ImageTranslation";
    public const string ReportStyleTranslation = "ReportStyleTranslation";
    public const string WritingAssistant = "WritingAssistant";
    public const string UserProfile = "UserProfile";

    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
    private static readonly object Sync = new();

    /// <summary>Reads a template by file name (without extension), cached after the first read.</summary>
    public static string Load(string name)
    {
        lock (Sync)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.Prompts.{name}.md";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Prompt template not found: {resourceName}");
            using var reader = new StreamReader(stream);

            var content = reader.ReadToEnd().Replace("\r\n", "\n").Trim();
            Cache[name] = content;
            return content;
        }
    }

    /// <summary>Loads a template and substitutes every {{KEY}} with its value, then tidies blank runs.</summary>
    public static string Render(string name, IReadOnlyDictionary<string, string> values)
    {
        var text = Load(name);
        foreach (var (key, value) in values)
            text = text.Replace($"{{{{{key}}}}}", value ?? string.Empty, StringComparison.Ordinal);

        return Regex.Replace(text, "\n{3,}", "\n\n").Trim();
    }

    /// <summary>Builds an optional titled block, or an empty string when there is no body to show.</summary>
    public static string Section(string heading, string? body) =>
        string.IsNullOrWhiteSpace(body) ? string.Empty : $"{heading}\n{body.Trim()}\n";
}
