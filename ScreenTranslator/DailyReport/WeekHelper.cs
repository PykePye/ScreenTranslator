using System.Globalization;

namespace ScreenTranslator.DailyReport;

/// <summary>
/// Tính tuần theo chuẩn ISO (tuần bắt đầu thứ Hai) + đặt tên file tuần.
/// Số hiển thị = thứ tự tuần trong tháng (1st..5th), khớp cách đặt tên cũ ("3rd-June-2026").
/// </summary>
public static class WeekHelper
{
    /// <summary>Thứ Hai của tuần ISO chứa <paramref name="date"/>.</summary>
    public static DateTime GetMonday(DateTime date)
    {
        int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.Date.AddDays(-diff);
    }

    /// <summary>6 ngày làm việc Mon..Sat của tuần.</summary>
    public static DateTime[] WorkDates(DateTime monday)
        => Enumerable.Range(0, 6).Select(i => monday.AddDays(i)).ToArray();

    /// <summary>Tuần thứ mấy trong tháng (tính theo ngày thứ Hai của tuần).</summary>
    public static int OrdinalOfMonth(DateTime monday)
        => ((monday.Day - 1) / 7) + 1;

    public static string OrdinalSuffix(int n)
    {
        if (n is 11 or 12 or 13) return n + "th";
        return (n % 10) switch
        {
            1 => n + "st",
            2 => n + "nd",
            3 => n + "rd",
            _ => n + "th"
        };
    }

    /// <summary>Nhãn tuần, ví dụ "3rd-June-2026".</summary>
    public static string WeekLabel(DateTime monday)
        => $"{OrdinalSuffix(OrdinalOfMonth(monday))}-{monday.ToString("MMMM", CultureInfo.InvariantCulture)}-{monday.Year}";

    /// <summary>Tên file tuần, ví dụ "Daily report - Phi 3rd-June-2026.xlsx".</summary>
    public static string BuildFileName(string reporter, DateTime monday)
        => $"Daily report - {reporter} {WeekLabel(monday)}.xlsx";
}
