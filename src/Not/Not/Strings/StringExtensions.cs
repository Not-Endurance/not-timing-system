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
}
