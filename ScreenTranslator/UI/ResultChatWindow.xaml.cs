using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using ScreenTranslator.History;

namespace ScreenTranslator.UI;

public partial class ResultChatWindow : Window
{
    public ObservableCollection<TranslationMessage> Messages { get; } = new();

    public event EventHandler? RequestSnip;
    public event EventHandler? RequestReload;

    public ResultChatWindow()
    {
        InitializeComponent();
        Icon = IconGenerator.GetAppIconSource();
        ChatItemsControl.ItemsSource = Messages;
        
        // Position at bottom right of screen
        var workingArea = SystemParameters.WorkArea;
        Left = workingArea.Right - Width - 20;
        Top = workingArea.Bottom - Height - 20;

        Closing += (s, e) => 
        {
            e.Cancel = true;
            Hide();
        };
    }

    public void LoadHistory(IEnumerable<HistoryEntry> history)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => 
        {
            Messages.Clear();
            foreach (var entry in history.Reverse())
            {
                Messages.Add(new TranslationMessage
                {
                    Timestamp = DateTime.Parse(entry.CreatedAt),
                    TranslatedText = entry.TranslatedText,
                    IsError = entry.IsError == 1,
                    ImageSnippet = (entry.ImageBlob != null && entry.ImageBlob.Length > 0) 
                                   ? LoadBitmap(entry.ImageBlob) 
                                   : null
                });
            }
            ChatScrollViewer.ScrollToEnd();
        });
    }

    private BitmapSource? LoadBitmap(byte[] blob)
    {
        try
        {
            using var ms = new MemoryStream(blob);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    public void AddMessage(TranslationMessage msg)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => 
        {
            Messages.Add(msg);
            ChatScrollViewer.ScrollToEnd();
        });
    }

    public void SetStatus(string text, string state = "Ready")
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => 
        {
            StatusText.Text = text;
            switch (state.ToLower())
            {
                case "busy":
                    StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 204, 0)); // Yellow
                    break;
                case "error":
                    StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 59, 48)); // Red
                    break;
                default:
                    StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 199, 89)); // Green
                    break;
            }
        });
    }

    private void Snip_Click(object sender, RoutedEventArgs e)
    {
        RequestSnip?.Invoke(this, EventArgs.Empty);
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        RequestReload?.Invoke(this, EventArgs.Empty);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string text)
        {
            System.Windows.Clipboard.SetText(text);
        }
    }
}