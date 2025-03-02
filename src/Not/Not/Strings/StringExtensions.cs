namespace Not.Strings;

public static class StringExtensions
{
    public static bool NContains(this string str, string term)
    {
        return str.Contains(term, StringComparison.InvariantCultureIgnoreCase);
    }
}
