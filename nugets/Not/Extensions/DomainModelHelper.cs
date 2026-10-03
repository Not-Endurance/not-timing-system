namespace Not.Extensions;

public static class DomainModelHelper
{
    public static string Combine(params object?[] values)
    {
        var sections = values.Where(x => x != null);
        return string.Join(" | ", sections);
    }
}
