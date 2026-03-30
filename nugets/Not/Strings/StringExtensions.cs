using System.Text;
using Not.Notify;

namespace Not.Strings;

public static class StringExtensions
{
    /// <summary>
    /// Contains with <see cref="StringComparison.InvariantCultureIgnoreCase"/> option
    /// </summary>
    /// <param name="str">Orginal string</param>
    /// <param name="term">Search term</param>
    /// <returns</returns>
    public static bool NContains(this string? str, string? term)
    {
        if (term == null)
        {
            return false;
        }
        return str?.Contains(term, StringComparison.InvariantCultureIgnoreCase) ?? false;
    }

    public static string NTrim(this string str, int maxLength)
    {
        if (str.Length <= maxLength)
        {
            return str;
        }
        var sb = new StringBuilder();
        for (var i = 0; i < maxLength; i++)
        {
            sb.Append(str[i]);
        }
        sb.AppendLine("...");
        return sb.ToString();
    }

    public static string Format(this string format, params object?[] args)
    {
        try
        {
            var normalized = args.Select(x => x?.ToString() ?? "");
            return string.Format(format, [.. normalized]);
        }
        catch (FormatException)
        {
            var message =
                Text_formatting_failed_This_is_usually_not_critical_failure_string
                + Environment.NewLine
                + $"Format: {format}"
                + Environment.NewLine
                + $"args: {string.Join(", ", args)}";
            Notifier?.Error(message);
            return format;
        }
    }

    static INotifier? Notifier => NotificationHelper.Current;
}
