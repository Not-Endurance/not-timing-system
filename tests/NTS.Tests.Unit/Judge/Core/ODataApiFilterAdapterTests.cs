using Not.Application.HTTP;

namespace NTS.Judge.Tests.Core;

public class ODataApiFilterAdapterTests
{
    [Fact]
    public void TryParseFilter_CreatesODataFilterFromExpression()
    {
        var eventId = 14;

        var isCreated = ODataApiFilterAdapter.TryParseFilters<QueryDocument>(
            [x => x.EventId == eventId && x.Name == "Open ride"],
            out var queryParameters
        );

        Assert.True(isCreated);
        Assert.Equal("EventId eq 14 and Name eq 'Open ride'", AssertFilter(queryParameters));
    }

    [Fact]
    public void TryParseFilter_EscapesODataStrings()
    {
        var isCreated = ODataApiFilterAdapter.TryParseFilters<QueryDocument>(
            [x => x.Name == "O'Brien"],
            out var queryParameters
        );

        Assert.True(isCreated);
        Assert.Equal("Name eq 'O''Brien'", AssertFilter(queryParameters));
    }

    [Fact]
    public void TryParseFilter_ReturnsEmptyQueryForTrueExpression()
    {
        var isCreated = ODataApiFilterAdapter.TryParseFilters<QueryDocument>([x => true], out var queryParameters);

        Assert.True(isCreated);
        Assert.Empty(queryParameters);

        queryParameters = ODataApiFilterAdapter.ParseFilters<QueryDocument>([x => true, x => x.Id == 7]);

        Assert.Equal("Id eq 7", AssertFilter(queryParameters));
    }

    [Fact]
    public void TryParseFilter_CreatesODataFilterFromComparisonOperators()
    {
        var queryParameters = ODataApiFilterAdapter.ParseFilters<QueryDocument>(
            [x => x.Id != 1, x => x.Id >= 2, x => x.Id <= 3, x => x.Id < 4, x => x.Id > 5]
        );

        Assert.Equal(
            "(Id ne 1) and (Id ge 2) and (Id le 3) and (Id lt 4) and (Id gt 5)",
            AssertFilter(queryParameters)
        );
    }

    [Fact]
    public void TryParseFilter_CreatesODataFilterFromListContains()
    {
        IList<int> ids = [1, 2];

        var isCreated = ODataApiFilterAdapter.TryParseFilters<QueryDocument>(
            [x => ids.Contains(x.Id)],
            out var queryParameters
        );

        Assert.True(isCreated);
        Assert.Equal("(Id eq 1 or Id eq 2)", AssertFilter(queryParameters));
    }

    [Fact]
    public void ParseFilters_JoinsMultipleExpressionFilters()
    {
        var queryParameters = ODataApiFilterAdapter.ParseFilters<QueryDocument>(
            [document => document.EventId == 14, document => document.Name == "O'Brien"]
        );

        Assert.Equal("(EventId eq 14) and (Name eq 'O''Brien')", AssertFilter(queryParameters));
    }

    [Fact]
    public void CombineQueryParameters_JoinsODataFilters()
    {
        var queryParameters = ODataApiFilterAdapter.ParseFilters<QueryDocument>(
            [document => document.EventId == 14, document => document.Id == 7]
        );

        Assert.Equal("(EventId eq 14) and (Id eq 7)", AssertFilter(queryParameters));
        Assert.Equal(
            "documents?%24filter=%28EventId%20eq%2014%29%20and%20%28Id%20eq%207%29",
            HttpHelper.AddQueryString("documents", queryParameters)
        );
    }

    static string AssertFilter(IReadOnlyDictionary<string, string> queryParameters)
    {
        var filter = Assert.Single(queryParameters);
        Assert.Equal("$filter", filter.Key);
        return filter.Value;
    }

    sealed class QueryDocument
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string? Name { get; set; }
    }
}
