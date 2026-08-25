using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenTranslator.DailyReport;

/// <summary>View-model cho một bong bóng việc trong cửa sổ chat.</summary>
public class ReportEntryVm : INotifyPropertyChanged
{
    public int Id { get; set; }

    private int _no;
    public int No
    {
        get => _no;
        set { _no = value; OnPropertyChanged(); }
    }

    private string _viText = string.Empty;
    public string ViText
    {
        get => _viText;
        set { _viText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasVi)); }
    }

    private string _enText = string.Empty;
    public string EnText
    {
        get => _enText;
        set { _enText = value; OnPropertyChanged(); }
    }

    public bool IsFixed { get; set; }

    public bool HasVi => !string.IsNullOrWhiteSpace(ViText);

    private bool _isEditing;
    /// <summary>True = đang ở chế độ sửa (hiện ô nhập + nút Lưu/Huỷ).</summary>
    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    /// <summary>Bản EN trước khi sửa — dùng để khôi phục khi bấm Huỷ.</summary>
    public string EnBackup { get; set; } = string.Empty;

    public static ReportEntryVm From(ReportEntry e) => new()
    {
        Id = e.Id,
        No = e.No,
        ViText = e.ViText,
        EnText = e.EnText,
        IsFixed = e.IsFixed == 1
    };

    public ReportEntry ToEntry(string workDate) => new()
    {
        Id = Id,
        WorkDate = workDate,
        No = No,
        ViText = ViText,
        EnText = EnText,
        IsFixed = IsFixed ? 1 : 0
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
