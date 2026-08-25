namespace ScreenTranslator.Settings;

public class AppSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultTargetLanguage { get; set; } = "Vietnamese";
    public string Model { get; set; } = "gemini-2.5-flash"; // Default to discovered model
    public string Hotkey { get; set; } = "D";
    public int HistoryRetentionLimit { get; set; } = 500;

    // Daily Report feature
    public string ReportOutputFolder { get; set; } = @"F:\Test_Result\Daily Report";
    public string ReporterName { get; set; } = "Phi";
}