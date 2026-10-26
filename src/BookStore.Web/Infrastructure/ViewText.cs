using System.Globalization;

namespace BookStore.Web.Infrastructure;

public static class ViewText
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static bool IsEnglish =>
        string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase);

    public static string CurrentCultureCode => IsEnglish ? "en" : "vi";

    public static string T(string vi, string en) => IsEnglish ? en : vi;

    public static string FormatVnd(decimal amount)
    {
        var wholeVnd = decimal.Truncate(amount);
        var formatted = wholeVnd.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN"));
        return $"{formatted} đ";
    }

    public static string FormatVietnamTime(DateTime utcDateTime, string format = "dd/MM/yyyy HH:mm")
    {
        var normalizedUtc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, VietnamTimeZone);
        return $"{vietnamTime.ToString(format, CultureInfo.InvariantCulture)} (UTC+7)";
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
            {
                return tz;
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "ICT",
            TimeSpan.FromHours(7),
            "Indochina Time (UTC+7)",
            "Indochina Time");
    }
}
