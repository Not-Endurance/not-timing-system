using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The query of a search by name, read when the parameter is bound: <c>filter=contains(name,'text')</c> with its sort and its
/// page and nothing else, or the error that refuses it. A text with no word long enough to make it a search and not a listing
/// is refused with the content, after the caller has been asked what they hold.
/// </summary>
internal sealed class SearchQuery : IJsonApiInput
{
    const string NAME = "name";

    public static ValueTask<SearchQuery?> BindAsync(HttpContext context)
    {
        return new ValueTask<SearchQuery?>(new SearchQuery(JsonApiQuery.ReadSearch(context.Request, NAME)));
    }

    SearchQuery(JsonApiSearch search)
    {
        Search = search;
    }

    /// <summary>The search, once the query is whole and its text is long enough.</summary>
    public JsonApiSearch Search { get; }

    public IResult? Transport => Search.Refusal;

    public string? NamedEvent => null;

    public string? DocumentId => null;

    public JsonApiFailure? Content()
    {
        return NameSearch.HasLongWord(Search.Words!) ? null : TenancyFailures.SearchTooShort;
    }
}
