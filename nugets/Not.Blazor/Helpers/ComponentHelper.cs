namespace Not.Blazor.Helpers;

public static class ComponentHelper
{
    public static string CombineStyles(params IEnumerable<string?> styles)
    {
        var normalized = styles.Where(x => !string.IsNullOrEmpty(x));
        return string.Join(";", normalized);
    }
}
