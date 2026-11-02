using Not.Application.HTTP;

namespace NTS.Tests.Unit.Not.Application.HTTP;

/// <summary>A list read a page at a time asks each page with the filter, the sort and the size it started with.</summary>
public sealed class ListQueryTests
{
    [Fact]
    public void A_later_page_of_a_list_keeps_its_filter_sort_and_size()
    {
        var first = new ListQuery(filter: "eventId eq 3", sort: "-name", pageSize: 500, pageNumber: 1);

        var second = first.OnPage(2);

        Assert.Equal("eventId eq 3", second.Filter);
        Assert.Equal("-name", second.Sort);
        Assert.Equal(500, second.PageSize);
        Assert.Equal(2, second.PageNumber);
        Assert.Equal(1, first.PageNumber);
    }
}
