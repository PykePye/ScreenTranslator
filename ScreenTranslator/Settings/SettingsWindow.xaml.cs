using System.Windows;
using System.Windows.Controls;
using ScreenTranslator.Translation;
using ScreenTranslator.UI;

namespace ScreenTranslator.Settings;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = IconGenerator.GetAppIconSource();
        _settings = SettingsStore.Load();
        LoadSettingsToUI();
    }

    private void LoadSettingsToUI()
    {
        ApiKeyBox.Password = _settings.ApiKey;
        
        foreach (ComboBoxItem item in TargetLanguageCombo.Items)
        {
            if (item.Content.ToString() == _settings.DefaultTargetLanguage)
            {
                TargetLanguageCombo.SelectedItem = item;
                break;
            }
        }

        foreach (ComboBoxItem item in ModelCombo.Items)
        {
            if (item.Content.ToString() == _settings.Model)
            {
                ModelCombo.SelectedItem = item;
                break;
            }
        }

        foreach (ComboBoxItem item in HotkeyCombo.Items)
        {
            if (item.Content.ToString() == _settings.Hotkey)
            {
                HotkeyCombo.SelectedItem = item;
                break;
            }
        }
        if (HotkeyCombo.SelectedItem == null) HotkeyCombo.SelectedIndex = 0;
    }

    public bool IsSuccess { get; private set; }

    private async void OnTestKeyClick(object sender, RoutedEventArgs e)
    {
        var tempKey = ApiKeyBox.Password;
        var tempModel = (ModelCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "gemini-2.5-flash";
        
        TestStatusText.Text = "Đang kiểm tra kết nối...";
        TestStatusText.Foreground = System.Windows.Media.Brushes.Blue;

        var service = new TranslationService(tempKey, tempModel);
        var result = await service.TestConnectionAsync();

        TestStatusText.Text = result;
        TestStatusText.Foreground = result.StartsWith("OK") 
            ? System.Windows.Media.Brushes.Green 
            : System.Windows.Media.Brushes.Red;
    }

    private async void OnListModelsClick(object sender, RoutedEventArgs e)
    {
        var tempKey = ApiKeyBox.Password;
        TestStatusText.Text = "Đang lấy danh sách model...";
        TestStatusText.Foreground = System.Windows.Media.Brushes.Blue;

        var service = new TranslationService(tempKey, "");
        var result = await service.ListModelsAsync();

        TestStatusText.Text = result;
        TestStatusText.Foreground = result.StartsWith("Danh sách") 
            ? System.Windows.Media.Brushes.Green 
            : System.Windows.Media.Brushes.Red;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _settings.ApiKey = ApiKeyBox.Password;
        _settings.DefaultTargetLanguage = (TargetLanguageCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Vietnamese";
        _settings.Model = (ModelCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "gemini-2.5-flash";
        _settings.Hotkey = (HotkeyCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "D";
        
        SettingsStore.Save(_settings);
        IsSuccess = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        IsSuccess = false;
        Close();
    }
}