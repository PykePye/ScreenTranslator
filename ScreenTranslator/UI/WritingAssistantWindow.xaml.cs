using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenTranslator.Settings;
using ScreenTranslator.Translation;
using ScreenTranslator.Writing;

namespace ScreenTranslator.UI;

public partial class WritingAssistantWindow : Window
{
    private readonly Func<AppSettings> _settingsProvider;
    private readonly WritingHistoryStore _historyStore;

    public ObservableCollection<WritingChatMessage> Messages { get; } = [];

    /// <summary>Recipient options bound to the picker above the input box.</summary>
    public IReadOnlyList<WritingAudience> Audiences => WritingAudience.All;

    public WritingAssistantWindow(Func<AppSettings> settingsProvider, WritingHistoryStore historyStore)
    {
        _settingsProvider = settingsProvider;
        _historyStore = historyStore;
        InitializeComponent();
        Icon = IconGenerator.GetAppIconSource();
        DataContext = this;

        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    public void PositionAbove(Window languageAssistantWindow)
    {
        const double gap = 8;
        var workingArea = SystemParameters.WorkArea;

        Left = languageAssistantWindow.Left + languageAssistantWindow.Width - Width;
        Top = Math.Max(workingArea.Top, languageAssistantWindow.Top - Height - gap);
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SubmitAsync();

    private async void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await SubmitAsync();
        }
    }

    private async Task SubmitAsync()
    {
        var original = InputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(original)) return;

        var settings = _settingsProvider();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            StatusText.Text = "Hãy thiết lập Gemini API Key trong Settings trước.";
            return;
        }

        SendButton.IsEnabled = false;
        InputBox.IsEnabled = false;
        StatusText.Text = "Đang cải thiện câu chữ...";
        Messages.Add(new WritingChatMessage
        {
            IsUser = true,
            Content = original
        });
        InputBox.Clear();
        ChatScrollViewer.ScrollToEnd();

        try
        {
            var audience = AudienceBox.SelectedItem as WritingAudience ?? WritingAudience.Colleague;
            var recurring = _historyStore.GetTopPatterns();

            var service = new TranslationService(settings.ApiKey, settings.Model);
            var result = await service.ImproveWritingAsync(original, audience, recurring);
            var saved = _historyStore.Save(original, result);

            Messages.Add(new WritingChatMessage
            {
                Minimal = result.Minimal,
                Casual = result.Casual,
                Professional = result.Professional,
                Issues = result.Mistakes.Select(CreateIssueView).ToList(),
                Reminders = saved.Reminders.ToList(),
                ChatId = saved.ChatId
            });
            StatusText.Text = "Chat and mistake histories have been saved.";
            ChatScrollViewer.ScrollToEnd();
        }
        catch (Exception ex)
        {
            Messages.Add(new WritingChatMessage
            {
                IsError = true,
                Error = $"Không thể xử lý: {ex.Message}"
            });
            StatusText.Text = "Xử lý thất bại.";
            ChatScrollViewer.ScrollToEnd();
        }
        finally
        {
            SendButton.IsEnabled = true;
            InputBox.IsEnabled = true;
            InputBox.Focus();
        }
    }

    private static WritingIssueView CreateIssueView(WritingMistake mistake)
    {
        var markerBrush = mistake.Group switch
        {
            1 => System.Windows.Media.Brushes.Red,
            2 => System.Windows.Media.Brushes.Goldenrod,
            _ => System.Windows.Media.Brushes.DodgerBlue
        };
        return new WritingIssueView
        {
            MarkerBrush = markerBrush,
            OriginalFragment = string.IsNullOrWhiteSpace(mistake.OriginalFragment) ? "—" : mistake.OriginalFragment.Trim(),
            Description = mistake.Description.Trim()
        };
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string text } && !string.IsNullOrWhiteSpace(text))
            System.Windows.Clipboard.SetText(text);
    }
}

public sealed class WritingChatMessage
{
    public bool IsUser { get; init; }
    public string Content { get; init; } = string.Empty;
    public string Minimal { get; init; } = string.Empty;
    public string Casual { get; init; } = string.Empty;
    public string Professional { get; init; } = string.Empty;
    public IReadOnlyList<WritingIssueView> Issues { get; init; } = [];
    public IReadOnlyList<string> Reminders { get; init; } = [];
    public string ChatId { get; init; } = string.Empty;
    public bool IsError { get; init; }
    public string Error { get; init; } = string.Empty;
}

public sealed class WritingIssueView
{
    public System.Windows.Media.Brush MarkerBrush { get; init; } = System.Windows.Media.Brushes.DodgerBlue;
    public string OriginalFragment { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
