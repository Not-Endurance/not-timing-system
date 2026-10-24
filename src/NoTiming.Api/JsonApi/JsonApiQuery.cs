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
