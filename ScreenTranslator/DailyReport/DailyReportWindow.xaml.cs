using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ScreenTranslator.Settings;
using ScreenTranslator.Translation;

namespace ScreenTranslator.DailyReport;

public partial class DailyReportWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ReportRepository _repo;
    private bool _loaded;

    public ObservableCollection<ReportEntryVm> Entries { get; } = new();

    public DailyReportWindow(AppSettings settings, ReportRepository repo)
    {
        InitializeComponent();
        _settings = settings;
        _repo = repo;
        EntriesList.ItemsSource = Entries;

        Loaded += OnLoaded;
        DayPicker.SelectedDateChanged += (_, _) => { if (_loaded) LoadDay(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TryAutoImportStyle();
        RefreshTemplates();
        DayPicker.SelectedDate ??= DateTime.Today;
        _loaded = true;
        LoadDay();
    }

    private DateTime CurrentDate => DayPicker.SelectedDate ?? DateTime.Today;
    private string WorkDateKey => CurrentDate.ToString("yyyy-MM-dd");

    private void LoadDay()
    {
        _repo.SeedFixedTasks(WorkDateKey);

        Entries.Clear();
        foreach (var entry in _repo.GetEntries(WorkDateKey))
            Entries.Add(ReportEntryVm.From(entry));

        var monday = WeekHelper.GetMonday(CurrentDate);
        WeekLabelText.Text = $"Tuần: {WeekHelper.WeekLabel(monday)}  ({CurrentDate:dddd})";
        EntriesScroll.ScrollToEnd();
    }

    // ---------- Add via AI ----------

    private async void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        var vn = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(vn)) return;

        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            System.Windows.MessageBox.Show("Vui lòng thiết lập Gemini API Key trong Settings trước.", "Daily Report");
            return;
        }

        AddBtn.IsEnabled = false;
        SetStatus("Đang dịch…", "busy");
        try
        {
            var en = await Translate(vn);

            var entry = new ReportEntry
            {
                WorkDate = WorkDateKey,
                No = _repo.NextNo(WorkDateKey),
                ViText = vn,
                EnText = en,
                IsFixed = 0
            };
            entry.Id = _repo.AddEntry(entry);

            Entries.Add(ReportEntryVm.From(entry));
            InputBox.Clear();
            EntriesScroll.ScrollToEnd();
            SetStatus("Đã thêm việc.", "ready");
        }
        catch (Exception ex)
        {
            SetStatus("Lỗi dịch thuật.", "error");
            System.Windows.MessageBox.Show($"Lỗi dịch: {ex.Message}", "Daily Report");
        }
        finally
        {
            AddBtn.IsEnabled = true;
        }
    }

    private async Task<string> Translate(string vn)
    {
        var translator = new TranslationService(_settings.ApiKey, _settings.Model);
        return await translator.TranslateTextStyledAsync(vn, _repo.GetStyleSample(25), _repo.GetGlossary());
    }

    // ---------- Per-entry actions ----------

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ReportEntryVm vm)
        {
            vm.EnBackup = vm.EnText;   // lưu để Huỷ khôi phục
            vm.IsEditing = true;
        }
    }

    private void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ReportEntryVm vm)
        {
            vm.IsEditing = false;
            _repo.UpdateEntry(vm.ToEntry(WorkDateKey));
            SetStatus("Đã lưu bản dịch chính thức.", "ready");
        }
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ReportEntryVm vm)
        {
            vm.EnText = vm.EnBackup;    // khôi phục, không lưu
            vm.IsEditing = false;
        }
    }

    private async void TranslateAgain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ReportEntryVm vm) return;
        if (!vm.HasVi) return;
        if (string.IsNullOrEmpty(_settings.ApiKey))
        {
            System.Windows.MessageBox.Show("Vui lòng thiết lập Gemini API Key trong Settings trước.", "Daily Report");
            return;
        }

        SetStatus("Đang dịch lại…", "busy");
        try
        {
            vm.EnText = await Translate(vm.ViText);
            _repo.UpdateEntry(vm.ToEntry(WorkDateKey));
            SetStatus("Đã dịch lại.", "ready");
        }
        catch (Exception ex)
        {
            SetStatus("Lỗi dịch thuật.", "error");
            System.Windows.MessageBox.Show($"Lỗi dịch: {ex.Message}", "Daily Report");
        }
    }

    private void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ReportEntryVm vm) return;
        _repo.DeleteEntry(vm.Id);
        _repo.Renumber(WorkDateKey);
        LoadDay();
    }

    // ---------- Templates ----------

    private void RefreshTemplates()
    {
        TemplateCombo.ItemsSource = _repo.GetTemplates();
        if (TemplateCombo.Items.Count > 0) TemplateCombo.SelectedIndex = 0;
    }

    private void InsertTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is not TaskTemplate t) return;

        if (!string.IsNullOrWhiteSpace(t.EnText))
        {
            var entry = new ReportEntry
            {
                WorkDate = WorkDateKey,
                No = _repo.NextNo(WorkDateKey),
                ViText = t.ViText,
                EnText = t.EnText,
                IsFixed = 0
            };
            entry.Id = _repo.AddEntry(entry);
            Entries.Add(ReportEntryVm.From(entry));
            EntriesScroll.ScrollToEnd();
            SetStatus("Đã chèn mẫu.", "ready");
        }
        else
        {
            InputBox.Text = t.ViText; // chỉ có tiếng Việt → đưa vào ô nhập để dịch
            InputBox.Focus();
        }
    }

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        var vn = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(vn))
        {
            System.Windows.MessageBox.Show("Nhập nội dung việc ở ô bên dưới trước khi lưu thành mẫu.", "Daily Report");
            return;
        }

        var title = vn.Length > 30 ? vn[..30] + "…" : vn;
        _repo.AddTemplate(new TaskTemplate { Title = title, ViText = vn, EnText = string.Empty });
        RefreshTemplates();
        SetStatus("Đã lưu mẫu mới.", "ready");
    }

    // ---------- Import style / Export ----------

    private void ImportBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx",
            Title = "Chọn file Excel báo cáo cũ để nạp kho mẫu phong cách"
        };
        if (File.Exists(ReportDefaults.DefaultStyleSourceFile))
            dlg.FileName = ReportDefaults.DefaultStyleSourceFile;

        if (dlg.ShowDialog() != true) return;

        try
        {
            var added = StyleImporter.ImportFromFile(dlg.FileName, _repo);
            System.Windows.MessageBox.Show($"Đã nạp {added} câu mẫu mới. Tổng kho hiện có {_repo.StyleCount()} câu.", "Daily Report");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Lỗi import: {ex.Message}", "Daily Report");
        }
    }

    private void ExportBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // chốt mọi chỉnh sửa đang mở trước khi xuất
            foreach (var vm in Entries)
                _repo.UpdateEntry(vm.ToEntry(WorkDateKey));

            var exporter = new ReportExcelExporter(_repo);
            var path = exporter.ExportWeek(CurrentDate, _settings.ReportOutputFolder, _settings.ReporterName);

            var res = System.Windows.MessageBox.Show(
                $"Đã xuất file:\n{path}\n\nMở thư mục chứa file?",
                "Daily Report", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Information);

            if (res == System.Windows.MessageBoxResult.Yes)
                Process.Start("explorer.exe", $"/select,\"{path}\"");

            SetStatus("Đã xuất tuần.", "ready");
        }
        catch (Exception ex)
        {
            SetStatus("Lỗi xuất file.", "error");
            System.Windows.MessageBox.Show($"Lỗi xuất Excel: {ex.Message}", "Daily Report");
        }
    }

    private void TryAutoImportStyle()
    {
        try
        {
            if (_repo.StyleCount() == 0 && File.Exists(ReportDefaults.DefaultStyleSourceFile))
                StyleImporter.ImportFromFile(ReportDefaults.DefaultStyleSourceFile, _repo);
        }
        catch
        {
            // im lặng — người dùng có thể import thủ công sau
        }
    }

    private void SetStatus(string text, string state)
    {
        StatusText.Text = text;
        StatusDot.Fill = state switch
        {
            "busy" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 204, 0)),
            "error" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 59, 48)),
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 199, 89)),
        };
    }
}
