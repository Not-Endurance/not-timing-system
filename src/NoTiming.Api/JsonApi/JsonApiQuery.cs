using System.Text.RegularExpressions;

namespace NoTiming.Api.JsonApi;

/// <summary>
/// The query string of a read, as the rest-api skill defines it: <c>filter</c> holds one OData <c>$filter</c>
/// expression, <c>sort</c> and <c>page[...]</c> are the ones of every list, and any other parameter answers 400 until a
/// consumer needs it. The filter is the part of the grammar that the first consumers use: members compared with <c>eq</c> to
/// a literal and joined by <c>and</c>, for the members a route allows. Anything else is refused as an invalid filter, so a
/// route never applies an expression it does not understand, and a member that a route does not name (the owner of a record,
/// say) cannot be asked for.
/// </summary>
internal static partial class JsonApiQuery
{
    const string FILTER = "filter";
    const string SORT = "sort";

    /// <summary>
    /// Reads the query of a list of the rows that one person or one Event keeps: the filter, which is members of the route
    /// compared with <c>eq</c> and joined by <c>and</c>, the <c>sort</c> of the member the route names (<c>member</c>, or
    /// <c>-member</c> the other way round; as it comes when there is none) and the pages of every list, <c>page[size]</c> from 1
    /// to <see cref="JsonApiList.MAX_PAGE_SIZE"/> and <c>page[number]</c>. A parameter that the route does not take is 400
    /// <c>unsupported-parameter</c>, a filter that is not valid for the members given 400 <c>invalid-filter</c>, a sort of
    /// another member 400 <c>invalid-sort</c> and a page that is not one 400 <c>invalid-page</c>.
    /// </summary>
    public static JsonApiList ReadList(HttpRequest request, string sortMember, params string[] filterMembers)
    {
        if (Unsupported(request, FILTER, SORT, JsonApiPaging.PAGE_SIZE, JsonApiPaging.PAGE_NUMBER) is { } unsupported)
        {
            return JsonApiList.Refused(unsupported);
        }

        var filter = ReadFilter(request, filterMembers);
        if (filter.Refusal != null)
        {
            return JsonApiList.Refused(filter.Refusal);
        }

        return
            ReadOrderAndPage(
                request,
                sortMember,
                JsonApiList.DEFAULT_PAGE_SIZE,
                JsonApiList.MAX_PAGE_SIZE,
                out var descending,
                out var size,
                out var number
            )
                is { } refusal
            ? JsonApiList.Refused(refusal)
            : JsonApiList.Of(filter.EqualTo, descending, size, number);
    }

    /// <summary>
    /// Reads the query of a search: <c>filter=contains(member,'text')</c>, the text as it is written (a quote in it is
    /// doubled) and a few words long, the <c>sort</c> of the member (<c>member</c> or <c>-member</c>, the first by default) and
    /// the pages of every list, but of a size up to <see cref="JsonApiSearch.MAX_PAGE_SIZE"/> only. A search is not a
    /// listing, so a request without a filter is refused, and so is any other parameter, any other member and any other
    /// expression. How long the words have to be is for the route to say.
    /// </summary>
    public static JsonApiSearch ReadSearch(HttpRequest request, string member)
    {
        if (Unsupported(request, FILTER, SORT, JsonApiPaging.PAGE_SIZE, JsonApiPaging.PAGE_NUMBER) is { } unsupported)
        {
            return JsonApiSearch.Refused(unsupported);
        }

        var match = SearchPredicate().Match(request.Query[FILTER].ToString().Trim());
        if (!match.Success || match.Groups["member"].Value != member)
        {
            return JsonApiSearch.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "invalid-filter",
                    "The filter is not valid.",
                    $"Use contains({member},'text')."
                )
            );
        }

        var text = match.Groups["value"].Value.Replace("''", "'").Trim();
        if (text.Contains('\0'))
        {
            return JsonApiSearch.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "invalid-filter",
                    "The filter is not valid.",
                    "A text has no null character."
                )
            );
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (words.Count > JsonApiSearch.MAX_WORDS)
        {
            return JsonApiSearch.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "invalid-filter",
                    "The filter is not valid.",
                    $"Use at most {JsonApiSearch.MAX_WORDS} words."
                )
            );
        }

        return
            ReadOrderAndPage(
                request,
                member,
                JsonApiSearch.DEFAULT_PAGE_SIZE,
                JsonApiSearch.MAX_PAGE_SIZE,
                out var descending,
                out var size,
                out var number
            )
                is { } refusal
            ? JsonApiSearch.Refused(refusal)
            : JsonApiSearch.Of(words, descending, size, number);
    }

    /// <summary>The refusal of a request that has a parameter other than those the route takes; none when it has not.</summary>
    public static IResult? Unsupported(HttpRequest request, params string[] taken)
    {
        return request.Query.Keys.FirstOrDefault(x => !taken.Contains(x)) is { } parameter
            ? JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "unsupported-parameter",
                "The parameter is not supported.",
                parameter
            )
            : null;
    }

    /// <summary>
    /// Reads the <c>sort</c> of the member (<c>member</c>, or <c>-member</c> the other way round; as it comes when there is none) and
    /// the pages, <c>page[size]</c> up to the greatest size that the list takes and <c>page[number]</c>. The answer that refuses
    /// them, or none: a sort of another member is 400 <c>invalid-sort</c>, and a page that is not one 400 <c>invalid-page</c>.
    /// </summary>
    static IResult? ReadOrderAndPage(
        HttpRequest request,
        string member,
        int defaultSize,
        int maxSize,
        out bool descending,
        out int size,
        out int number
    )
    {
        descending = false;
        size = defaultSize;
        number = 1;
        if (request.Query.TryGetValue(SORT, out var sort))
        {
            if (sort.ToString() != member && sort.ToString() != "-" + member)
            {
                return JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "invalid-sort",
                    "The sort is not valid.",
                    $"Sort by {member}, with a - for descending: sort=-{member}."
                );
            }

            descending = sort.ToString().StartsWith('-');
        }

        return JsonApiPaging.Read(request, defaultSize, maxSize, out size, out number);
    }

    static JsonApiFilter ReadFilter(HttpRequest request, string[] members)
    {
        var filter = request.Query[FILTER].ToString();
        if (filter.Length == 0)
        {
            return JsonApiFilter.Of(new Dictionary<string, string>());
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var rest = filter.Trim();
        while (rest.Length > 0)
        {
            var match = Predicate().Match(rest);
            if (!match.Success || !members.Contains(match.Groups["member"].Value) || !Unique(values, match))
            {
                return JsonApiFilter.Refused(
                    JsonApiResults.Error(
                        StatusCodes.Status400BadRequest,
                        "invalid-filter",
                        "The filter is not valid.",
                        $"Use {string.Join(" and ", members.Select(x => $"{x} eq <value>"))}."
                    )
                );
            }

            rest = rest[match.Length..];
        }

        return JsonApiFilter.Of(values);
    }

    static bool Unique(Dictionary<string, string> values, Match match)
    {
        var literal = match.Groups["value"].Value;
        return values.TryAdd(
            match.Groups["member"].Value,
            literal.StartsWith('\'') ? literal[1..^1].Replace("''", "'") : literal
        );
    }

    [GeneratedRegex(@"^(?<member>[A-Za-z][A-Za-z0-9]*) eq (?<value>'(?:[^']|'')*'|[^\s']+)(?: and |$)")]
    private static partial Regex Predicate();

    [GeneratedRegex(@"^contains\((?<member>[A-Za-z][A-Za-z0-9]*)\s*,\s*'(?<value>(?:[^']|'')*)'\)$")]
    private static partial Regex SearchPredicate();
}

/// <summary>What the query string of a read asked for, or the answer that refuses it.</summary>
internal sealed class JsonApiFilter
{
    public static JsonApiFilter Of(IReadOnlyDictionary<string, string> equalTo)
    {
        return new JsonApiFilter(equalTo, null);
    }

    public static JsonApiFilter Refused(IResult refusal)
    {
        return new JsonApiFilter(new Dictionary<string, string>(), refusal);
    }

    JsonApiFilter(IReadOnlyDictionary<string, string> equalTo, IResult? refusal)
    {
        EqualTo = equalTo;
        Refusal = refusal;
    }

    /// <summary>The members the filter compares, and what each is compared with, as the text that was written.</summary>
    public IReadOnlyDictionary<string, string> EqualTo { get; }

    public IResult? Refusal { get; }
}

/// <summary>
/// What the query string of a list of the rows one person or one Event keeps asked for, or the answer that refuses it: the
/// filter, which way the rows are in the order, and the page that is wanted. A page is a hundred rows
/// unless it is asked to be another size, and none is more than five hundred.
/// </summary>
internal sealed class JsonApiList
{
    public const int DEFAULT_PAGE_SIZE = 100;
    public const int MAX_PAGE_SIZE = 500;

    public static JsonApiList Of(IReadOnlyDictionary<string, string> equalTo, bool descending, int size, int number)
    {
        return new JsonApiList(equalTo, descending, size, number, null);
    }

    public static JsonApiList Refused(IResult refusal)
    {
        return new JsonApiList(new Dictionary<string, string>(), false, DEFAULT_PAGE_SIZE, 1, refusal);
    }

    JsonApiList(IReadOnlyDictionary<string, string> equalTo, bool descending, int size, int number, IResult? refusal)
    {
        EqualTo = equalTo;
        Descending = descending;
        Size = size;
        Number = number;
        Refusal = refusal;
    }

    /// <summary>The members the filter compares, and what each is compared with, as the text that was written.</summary>
    public IReadOnlyDictionary<string, string> EqualTo { get; }

    /// <summary>Whether the rows are in the order the other way round.</summary>
    public bool Descending { get; }

    public int Size { get; }
    public int Number { get; }
    public IResult? Refusal { get; }

    /// <summary>How many rows to leave out.</summary>
    public int Skip => (Number - 1) * Size;
}

/// <summary>
/// What the query string of a search asked for, or the answer that refuses it: the words to look for, the direction of the
/// order of the names and the page that is wanted. A page is ten rows unless it is asked to be another size, and no search
/// answers more than twenty-five rows.
/// </summary>
internal sealed class JsonApiSearch
{
    public const int DEFAULT_PAGE_SIZE = 10;
    public const int MAX_PAGE_SIZE = 25;
    public const int MAX_WORDS = 6;

    public static JsonApiSearch Of(IReadOnlyList<string> words, bool descending, int size, int number)
    {
        return new JsonApiSearch(words, descending, size, number, null);
    }

    public static JsonApiSearch Refused(IResult refusal)
    {
        return new JsonApiSearch(null, false, DEFAULT_PAGE_SIZE, 1, refusal);
    }

    JsonApiSearch(IReadOnlyList<string>? words, bool descending, int size, int number, IResult? refusal)
    {
        Words = words;
        Descending = descending;
        Size = size;
        Number = number;
        Refusal = refusal;
    }

    /// <summary>The words of the text, once each whatever the case.</summary>
    public IReadOnlyList<string>? Words { get; }

    /// <summary>Whether the names are in the order the other way round.</summary>
    public bool Descending { get; }

    public int Size { get; }
    public int Number { get; }
    public IResult? Refusal { get; }

    /// <summary>How many rows to leave out and how many to read: one more than a page, to know whether another follows.</summary>
    public int Skip => (Number - 1) * Size;

    public int Take => Size + 1;
}
