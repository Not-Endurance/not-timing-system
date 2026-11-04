namespace Not.Formatting;

public static class FormattingHelper
{
    public static string Format(TimeSpan timeSpan)
    {
        return $"{timeSpan:hh\\:mm\\:ss}";
    }
}
