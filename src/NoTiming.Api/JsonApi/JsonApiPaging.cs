namespace NoTiming.Api.JsonApi;

/// <summary>
/// The pages of a list as the rest-api skill defines them: <c>page[size]</c> and <c>page[number]</c> in the query, the
/// <c>links</c> that say where the list is and where its next page is, and the <c>meta</c> that says which page it is. A list
/// that is paged says it this way whatever it is a list of, and reads one row more than a page to know whether another follows.
/// </summary>
internal static class JsonApiPaging
{
    public const string PAGE_SIZE = "page[size]";
    public const string PAGE_NUMBER = "page[number]";

    /// <summary>
    /// Reads the page a request names, whatever the list is of: its size from 1 to the greatest that the list takes
    /// (<paramref name="defaultSize"/> when it names none) and its number from 1. The answer that refuses it, 400
    /// <c>invalid-page</c>, or none when it is a page.
    /// </summary>
    public static IResult? Read(HttpRequest request, int defaultSize, int maxSize, out int size, out int number)
    {
        size = defaultSize;
        number = 1;
        return
            TryRead(request, PAGE_SIZE, maxSize, ref size)
            && TryRead(request, PAGE_NUMBER, int.MaxValue / maxSize, ref number)
            ? null
            : JsonApiFailures.InvalidPage(maxSize).AsResult();
    }

    /// <summary>Where the list is, and where its next page is when there is one.</summary>
    public static object Links(HttpRequest request, int number, bool hasNext)
    {
        var self = request.Path + request.QueryString;
        return new { self, next = hasNext ? WithPage(request, number + 1) : null };
    }

    /// <summary>Which page the list is in.</summary>
    public static object Meta(int size, int number)
    {
        return new { page = new { size, number } };
    }

    /// <summary>
    /// Reads a parameter of a page: true when the request does not name it, or names a whole number from 1 to the greatest that
    /// the list takes, which is then the value.
    /// </summary>
    static bool TryRead(HttpRequest request, string parameter, int max, ref int value)
    {
        if (!request.Query.TryGetValue(parameter, out var values))
        {
            return true;
        }

        if (!int.TryParse(values.ToString(), out var parsed) || parsed < 1 || parsed > max)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    /// <summary>The address of the request with another page: the rest of the query is kept.</summary>
    static string WithPage(HttpRequest request, int number)
    {
        var parameters = request
            .Query.Where(x => x.Key != PAGE_NUMBER)
            .SelectMany(x =>
                x.Value.Select(value => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(value ?? "")}")
            )
            .Append($"{Uri.EscapeDataString(PAGE_NUMBER)}={number}");
        return $"{request.Path}?{string.Join('&', parameters)}";
    }
}
