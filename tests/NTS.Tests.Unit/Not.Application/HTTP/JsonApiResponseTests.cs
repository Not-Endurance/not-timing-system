using System.Net;
using System.Text.Json;
using Not.Application.HTTP;

namespace NTS.Tests.Unit.Not.Application.HTTP;

/// <summary>What a client reads of a list that the Api cuts into pages: another page follows when the links say where it is, and for no other reason.</summary>
public sealed class JsonApiResponseTests
{
    [Theory]
    [InlineData("""{"data":[],"links":{"self":"/x","next":"/x?page%5Bnumber%5D=2"}}""", true)]
    [InlineData("""{"data":[],"links":{"self":"/x","next":null}}""", false)]
    [InlineData("""{"data":[],"links":{"self":"/x"}}""", false)]
    [InlineData("""{"data":[]}""", false)]
    [InlineData("""{"data":[],"links":"/x"}""", false)]
    [InlineData("""{"data":[],"links":{"next":7}}""", false)]
    [InlineData("""[]""", false)]
    public void Another_page_follows_only_when_the_links_say_where_it_is(string body, bool expected)
    {
        using var document = JsonDocument.Parse(body);
        var response = new JsonApiResponse(HttpStatusCode.OK, document.RootElement, null, null);

        Assert.Equal(expected, response.HasNextPage);
    }

    [Fact]
    public void An_answer_with_no_document_has_no_next_page()
    {
        var response = new JsonApiResponse(HttpStatusCode.NoContent, null, null, null);

        Assert.False(response.HasNextPage);
    }
}
