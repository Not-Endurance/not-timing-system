using EMS.Judge.Application.Common.Exceptions;
using System;
using System.Globalization;
using static EMS.Judge.DesktopConstants;
using static Core.Localization.Strings;

namespace EMS.Judge.Common;

public class ValueSerializer
{
    private const string DEFAULT_TIME = "--:--:--";
    private const string DEFAULT_SPAN = "-:-:-.-";
    private const string DEFAULT_DOUBLE = "-.---";

    public static DateTime? ParseTime(string value)
    {
        if (value is null or DEFAULT_TIME or "")
        {
            return null;
        }
        var hasParsed = DateTime.TryParseExact(
            value,
            TIME_FORMAT,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var result);
        if (!hasParsed)
        {
            var message = string.Format(INVALID_DATE_FORMAT_MESSAGE, value, TIME_FORMAT);
            throw new AppException(message);
        }
        return result;
    }
    public static TimeSpan? ParseSpan(string value)
    {
        if (value == null)
        {
            return null;
        }
        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            return timeSpan;
        }
        return null;
    }
    public static string FormatSpan(TimeSpan? span)
    {
        var spanString = span?.ToString(TIME_SPAN_FORMAT) ?? DEFAULT_SPAN;
        //spanString = spanString.PadRight(12, '0');
        return spanString;
    }
    public static string FormatTime(DateTime? time)
    {
        var timeString = time?.ToString(TIME_FORMAT) ?? DEFAULT_TIME;
        return timeString;
    }
    public static string FormatDouble(double? value)
    {
        var doubleString = value?.ToString(DOUBLE_FORMAT) ?? DEFAULT_DOUBLE;
        return doubleString;
    }
    public static double? ParseDouble(string value)
    {
        if (double.TryParse(value, out var number))
        {
            return number;
        }
        return null;
    }
}
