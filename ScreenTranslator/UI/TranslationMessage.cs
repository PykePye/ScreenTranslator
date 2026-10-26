using System.Windows.Media.Imaging;

namespace ScreenTranslator.UI;

public class TranslationMessage
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string OriginalText { get; set; } = string.Empty; // Empty for errors and for history saved before it was kept
    public string TranslatedText { get; set; } = string.Empty;
    public BitmapSource? ImageSnippet { get; set; }
    public bool IsError { get; set; }
}