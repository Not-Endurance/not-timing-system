using System.Text.RegularExpressions;

namespace NoTiming.Api.JsonApi;

/// <summary>
/// The query string of a read, as the rest-api skill defines it: <c>filter</c> holds one OData <c>$filter</c>
/// expression, and any other parameter answers 400 until a consumer needs it. This is the part of the filter grammar
/// that the first consumers use: members compared with <c>eq</c> to a literal and joined by <c>and</c>, for the members
/// a route allows. Anything else is refused as an invalid filter, so a route never applies an expression it does not
/// understand, and a member that a route does not name (the owner of a record, say) cannot be asked for.
/// </summary>
internal static partial class JsonApiQuery
{
    const string FILTER = "filter";
    const string SORT = "sort";

    /// <summary>
    /// Reads the parameters of the request. A parameter that the route does not take is 400 <c>unsupported-parameter</c>,
    /// and a filter that is not valid for the members given is 400 <c>invalid-filter</c>.
    /// </summary>
    public static JsonApiFilter Read(HttpRequest request, params string[] members)
    {
        foreach (var parameter in request.Query.Keys)
        {
            if (parameter != FILTER)
            {
                return JsonApiFilter.Refused(
                    JsonApiResults.Error(
                        StatusCodes.Status400BadRequest,
                        "unsupported-parameter",
                        "The parameter is not supported.",
                        parameter
                    )
                );
            }
        }

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

    /// <summary>
    /// Reads the query of a search: <c>filter=contains(member,'text')</c>, the text as it is written (a quote in it is
    /// doubled) and a few words long, the <c>sort</c> of the member (<c>member</c> or <c>-member</c>, the first by default) and
    /// the pages of every list, but of a size up to <see cref="JsonApiSearch.MAX_PAGE_SIZE"/> only. A search is not a
    /// listing, so a request without a filter is refused, and so is any other parameter, any other member and any other
    /// expression. How long the words have to be is for the route to say.
    /// </summary>
    public static JsonApiSearch ReadSearch(HttpRequest request, string member)
    {
        foreach (var parameter in request.Query.Keys)
        {
            if (parameter is not (FILTER or SORT or JsonApiPaging.PAGE_SIZE or JsonApiPaging.PAGE_NUMBER))
            {
                return JsonApiSearch.Refused(
                    JsonApiResults.Error(
                        StatusCodes.Status400BadRequest,
                        "unsupported-parameter",
                        "The parameter is not supported.",
                        parameter
                    )
                );
            }
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

        var descending = false;
        if (request.Query.TryGetValue(SORT, out var sort))
        {
            if (sort.ToString() != member && sort.ToString() != "-" + member)
            {
                return JsonApiSearch.Refused(
                    JsonApiResults.Error(
                        StatusCodes.Status400BadRequest,
                        "invalid-sort",
                        "The sort is not valid.",
                        $"Sort by {member}, with a - for descending: sort=-{member}."
                    )
                );
            }

            descending = sort.ToString().StartsWith('-');
        }

        var size = JsonApiSearch.DEFAULT_PAGE_SIZE;
        var number = 1;
        if (
            !JsonApiPaging.TryRead(request, JsonApiPaging.PAGE_SIZE, JsonApiSearch.MAX_PAGE_SIZE, ref size)
            || !JsonApiPaging.TryRead(
                request,
                JsonApiPaging.PAGE_NUMBER,
                int.MaxValue / JsonApiSearch.MAX_PAGE_SIZE,
                ref number
            )
        )
        {
            return JsonApiSearch.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "invalid-page",
                    "A page is a number of at least 1 and a size of 1 to 25."
                )
            );
        }

        return JsonApiSearch.Of(words, descending, size, number);
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
