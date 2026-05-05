using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;
using Application = System.Windows.Application;
using System.Windows.Media.Imaging;
using ScreenTranslator.UI;
using ScreenTranslator.Hotkey;
using ScreenTranslator.Snip;
using ScreenTranslator.Settings;
using ScreenTranslator.Translation;
using ScreenTranslator.History;

namespace ScreenTranslator;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _isPaused;
    private HotkeyManager? _hotkeyManager;
    private AppSettings _settings = new();
    private ResultChatWindow? _chatWindow;
    private HistoryRepository _historyRepo = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Global error handler
        DispatcherUnhandledException += (s, args) => 
        {
            System.Windows.MessageBox.Show($"Ứng dụng gặp lỗi bất ngờ: {args.Exception.Message}", "Critical Error");
            args.Handled = true;
            Shutdown();
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try 
        {
            _settings = SettingsStore.Load();
            _historyRepo = new HistoryRepository();
            
            _chatWindow = new ResultChatWindow();
            _chatWindow.LoadHistory(_historyRepo.GetLatest(50));
            
            // Wire up UI events
            _chatWindow.RequestSnip += (_, _) => OnHotkeyPressed(null, EventArgs.Empty);
            _chatWindow.RequestReload += (_, _) => ReloadLastTranslation();
            
            InitializeTrayIcon();
            EnsureAutoStart(true);
            InitializeHotkey();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Lỗi khi khởi động: {ex.Message}", "Startup Error");
            Shutdown();
        }
    }

    private void InitializeHotkey()
    {
        _hotkeyManager?.Dispose();
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

        var modifiers = HotkeyManager.Modifiers.Control | HotkeyManager.Modifiers.Shift;
        var vk = GetVirtualKey(_settings.Hotkey);
        var success = _hotkeyManager.Register(modifiers, vk);

        if (!success)
        {
            _trayIcon?.ShowBalloonTip(
                5000,
                "Screen Translator",
                $"Phím tắt Ctrl+Shift+{_settings.Hotkey} đang bị app khác chiếm.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private uint GetVirtualKey(string key)
    {
        return key.ToUpper() switch
        {
            "D" => 0x44,
            "Z" => 0x5A,
            "S" => 0x53,
            "Q" => 0x51,
            "F" => 0x46,
            _ => 0x44
        };
    }

    private SnipOverlayWindow? _activeOverlay;

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_isPaused) return;

        if (_activeOverlay != null)
        {
            _activeOverlay.Close();
            _activeOverlay = null;
            return;
        }

        var overlay = new SnipOverlayWindow();
        _activeOverlay = overlay;

        try
        {
            overlay.Closed += OnOverlayClosed;
            overlay.Show();
            overlay.Activate();
            overlay.Focus();
        }
        catch (Exception ex)
        {
            _activeOverlay = null;
            System.Windows.MessageBox.Show($"Overlay error: {ex.Message}");
        }
    }

    private async void ReloadLastTranslation()
    {
        var latest = _historyRepo.GetLatest(1).FirstOrDefault();
        if (latest == null || latest.ImageBlob == null || latest.ImageBlob.Length == 0)
        {
            System.Windows.MessageBox.Show("Không tìm thấy ảnh cũ để dịch lại.", "Thông báo");
            return;
        }

        await ProcessTranslation(latest.ImageBlob);
    }

    private async void OnOverlayClosed(object? sender, EventArgs e)
    {
        if (sender is not SnipOverlayWindow overlay) return;
        
        overlay.Closed -= OnOverlayClosed;
        
        var bitmap = overlay.CapturedBitmap;
        var wasSuccessful = overlay.IsSuccess && bitmap != null;
        
        _activeOverlay = null;
        MainWindow = null;
        
        if (wasSuccessful && bitmap != null)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            await ProcessTranslation(ms.ToArray());
        }
    }

    private async Task ProcessTranslation(byte[] imageBytes)
    {
        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            System.Windows.MessageBox.Show("Vui lòng thiết lập Gemini API Key trong Settings trước.");
            ShowSettings();
            return;
        }

        try
        {
            _chatWindow?.Show();
            _chatWindow?.Activate();
            _chatWindow?.SetStatus("Đang dịch...", "busy");

            using var ms = new MemoryStream(imageBytes);
            var bitmapSource = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            var translator = new TranslationService(_settings.ApiKey, _settings.Model);
            var translation = await translator.TranslateImageAsync(imageBytes, _settings.DefaultTargetLanguage, CancellationToken.None);

            System.Windows.Clipboard.SetText(translation);
            
            _historyRepo.AddEntry(new HistoryEntry
            {
                ImageBlob = imageBytes,
                TranslatedText = translation,
                IsError = 0
            });
            _historyRepo.Cleanup(_settings.HistoryRetentionLimit);

            _chatWindow?.AddMessage(new TranslationMessage 
            { 
                TranslatedText = translation,
                ImageSnippet = bitmapSource,
                IsError = false
            });
            _chatWindow?.SetStatus("Sẵn sàng", "ready");
        }
        catch (Exception ex)
        {
            _historyRepo.AddEntry(new HistoryEntry
            {
                TranslatedText = $"Lỗi: {ex.Message}",
                IsError = 1
            });

            _chatWindow?.AddMessage(new TranslationMessage 
            { 
                TranslatedText = $"Lỗi: {ex.Message}",
                IsError = true
            });
            _chatWindow?.SetStatus("Lỗi dịch thuật", "error");
        }
    }

    private void InitializeTrayIcon()
    {
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        var historyItem = new System.Windows.Forms.ToolStripMenuItem("Show History");
        historyItem.Click += (_, _) => { _chatWindow?.Show(); _chatWindow?.Activate(); };

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => ShowSettings();

        var pauseItem = new System.Windows.Forms.ToolStripMenuItem("Pause");
        pauseItem.Click += (s, _) => TogglePause((System.Windows.Forms.ToolStripMenuItem)s!);

        var separator = new System.Windows.Forms.ToolStripSeparator();

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApp();

        contextMenu.Items.Add(historyItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(pauseItem);
        contextMenu.Items.Add(separator);
        contextMenu.Items.Add(exitItem);

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = IconGenerator.GetAppIcon(),
            Text = "Trợ lý ngôn ngữ của Bryan",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _trayIcon.DoubleClick += (_, _) => { _chatWindow?.Show(); _chatWindow?.Activate(); };
    }

    private void ShowSettings()
    {
        var win = new SettingsWindow();
        win.Closed += (s, _) => {
            _settings = SettingsStore.Load();
            InitializeHotkey(); // Re-register if changed
            MainWindow = null;
        };
        win.Show();
    }

    private void TogglePause(System.Windows.Forms.ToolStripMenuItem item)
    {
        _isPaused = !_isPaused;
        item.Text = _isPaused ? "Resume" : "Pause";
    }

    private void ExitApp()
    {
        _hotkeyManager?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _chatWindow?.Close();
        Shutdown();
    }

    private void EnsureAutoStart(bool enable)
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "ScreenTranslator";

        using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
        if (key == null) return;

        if (enable)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName
                          ?? Assembly.GetExecutingAssembly().Location;
            key.SetValue(appName, $"\"{exePath}\"");
        }
        else
        {
            if (key.GetValue(appName) != null)
                key.DeleteValue(appName);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}