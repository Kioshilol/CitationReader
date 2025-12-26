using System.Globalization;

namespace CitationReader.Extensions;

public static class StringExtensions
{
    public static DateTime ParseToDateTime(this string? dateTimeString)
    {
        // Early return for null, empty, or whitespace strings
        if (string.IsNullOrWhiteSpace(dateTimeString))
        {
            return DateTime.MinValue;
        }

        // Try parsing with invariant culture first for better performance
        if (DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        // Fallback to current culture parsing
        if (DateTime.TryParse(dateTimeString, out result))
        {
            return result;
        }

        // Try common date formats if standard parsing fails
        var commonFormats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "yyyy/MM/dd",
            "yyyy/MM/dd HH:mm:ss",
            "yyyyMMdd",
            "yyyyMMddHHmmss"
        };

        foreach (var format in commonFormats)
        {
            if (DateTime.TryParseExact(dateTimeString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return result;
            }
        }

        return DateTime.MinValue;
    }
}
