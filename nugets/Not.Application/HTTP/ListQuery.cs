namespace Not.Application.HTTP;

/// <summary>
/// What a list is asked for (ADR-0008): its filter, an OData expression that the Api reads, its sort, and one page of it. The
/// page size is the one the route allows, and a list is read a page at a time by asking the same query for each page.
/// </summary>
public sealed class ListQuery
{
    public ListQuery(string? filter = null, string? sort = null, int? pageSize = null, int? pageNumber = null)
    {
        Filter = filter;
        Sort = sort;
        PageSize = pageSize;
        PageNumber = pageNumber;
    }

    public string? Filter { get; }
    public string? Sort { get; }
    public int? PageSize { get; }
    public int? PageNumber { get; }

    /// <summary>The same list, asked for the page with the given number.</summary>
    public ListQuery OnPage(int number)
    {
        return new ListQuery(Filter, Sort, PageSize, number);
    }
}
