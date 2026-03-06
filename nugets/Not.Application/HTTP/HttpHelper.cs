namespace Not.Application.HTTP;

public static class HttpHelper
{
    public static string NormalizeUri(string? uri)
    {
        if (uri == null)
        {
            return "";
        }
        if (uri.StartsWith('/'))
        {
            return string.Join("", uri.Skip(1));
        }
        if (uri.EndsWith('/'))
        {
            return string.Join("", uri.SkipLast(1));
        }
        return uri;
    }
}
