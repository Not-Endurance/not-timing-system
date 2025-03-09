using System.Text;

namespace Not.Strings;

public static class StringExtensions
{
    /// <summary>
    /// Contains with <see cref="StringComparison.InvariantCultureIgnoreCase"/> option
    /// </summary>
    /// <param name="str">Orginal string</param>
    /// <param name="term">Search term</param>
    /// <returns</returns>
    public static bool NContains(this string str, string term)
    {
        return str.Contains(term, StringComparison.InvariantCultureIgnoreCase);
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
}
