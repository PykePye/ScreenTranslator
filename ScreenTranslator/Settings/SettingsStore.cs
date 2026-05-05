using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScreenTranslator.Settings;

public class SettingsStore
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenTranslator");
    
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    // Entropy used for DPAPI (optional but recommended)
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ScreenTranslator-Secret-Entropy");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            
            if (settings != null && !string.IsNullOrEmpty(settings.ApiKey))
            {
                settings.ApiKey = Decrypt(settings.ApiKey);
            }
            
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
            {
                Directory.CreateDirectory(SettingsDir);
            }

            // Clone settings to avoid encrypting the live object's key
            var settingsToSave = new AppSettings
            {
                DefaultTargetLanguage = settings.DefaultTargetLanguage,
                Model = settings.Model,
                Hotkey = settings.Hotkey,
                HistoryRetentionLimit = settings.HistoryRetentionLimit,
                ApiKey = string.IsNullOrEmpty(settings.ApiKey) ? "" : Encrypt(settings.ApiKey)
            };

            var json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Lỗi lưu settings: {ex.Message}");
        }
    }

    private static string Encrypt(string plainText)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Decrypt(string encryptedText)
    {
        try
        {
            var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("placeholder")); // Dummy for structure if needed
            // Wait, the input is already base64
            var encryptedData = Convert.FromBase64String(encryptedText);
            var decrypted = ProtectedData.Unprotect(encryptedData, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }
}