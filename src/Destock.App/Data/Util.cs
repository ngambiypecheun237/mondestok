using System.Globalization;
using System.Text;

namespace Destock.App.Data;

internal static class Util
{
    public static string NowLocal()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string TodayIso()
    {
        return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string MonthKey(DateTime date)
    {
        return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    public static string MonthLabel(DateTime date)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        return date.ToString("MMM yyyy", culture);
    }

    public static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains(';') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
