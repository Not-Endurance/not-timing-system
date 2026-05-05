using Not.Application.HTTP;

namespace NTS.Judge.Tests.Core;

public class ODataApiFilterAdapterTests
{
    [Fact]
    public void TryParseFilter_CreatesODataFilterFromExpression()
    {
        var eventId = 14;

        var isCreated = ODataApiFilterAdapter.TryParseFilter<QueryDocument>(
            x => x.EventId == eventId && x.Name == "Open ride",
            out var queryParameters
        );

        Assert.True(isCreated);
        Assert.Equal(
            "EventId eq 14 and Name eq 'Open ride'",
            AssertFilter(queryParameters)
        );
    }

    [Fact]
    public void TryParseFilter_EscapesODataStrings()
    {
        var isCreated = ODataApiFilterAdapter.TryParseFilter<QueryDocument>(
            x => x.Name == "O'Brien",
            out var queryParameters
        );

        Assert.True(isCreated);
        Assert.Equal("Name eq 'O''Brien'", AssertFilter(queryParameters));
    }

    [Fact]
    public void TryParseFilter_ReturnsFalseForUnsupportedExpression()
    {
        int[] ids = [1, 2];

        var isCreated = ODataApiFilterAdapter.TryParseFilter<QueryDocument>(
            x => ids.Contains(x.Id),
            out var queryParameters
        );

        Assert.False(isCreated);
        Assert.Empty(queryParameters);
    }

    [Fact]
    public void ParseFilters_JoinsMultipleExpressionFilters()
    {
        var queryParameters = ODataApiFilterAdapter.ParseFilters<QueryDocument>(
            [
                document => document.EventId == 14,
                document => document.Name == "O'Brien",
            ]
        );

        Assert.Equal("(EventId eq 14) and (Name eq 'O''Brien')", AssertFilter(queryParameters));
    }

    [Fact]
    public void CombineQueryParameters_JoinsODataFilters()
    {
        var selectedEvent = ODataApiFilterAdapter.ParseFilters<QueryDocument>(
            [document => document.EventId == 14]
        );
        var readFilter = ODataApiFilterAdapter.ParseFilters<QueryDocument>(
            [document => document.Id == 7]
        );

        var queryParameters = ODataApiFilterAdapter.CombineQueryParameters(selectedEvent, readFilter);

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
