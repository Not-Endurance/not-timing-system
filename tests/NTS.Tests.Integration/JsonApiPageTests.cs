using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.JsonApi;

namespace NTS.Tests.Integration;

/// <summary>
/// The pages of a list (the rest-api skill): <c>page[size]</c> and <c>page[number]</c> are read in one place whatever the list is
/// of, and a list answers with its page, where it is and where the next one is, in one call. No host and no database.
/// </summary>
public sealed class JsonApiPageTests
{
    [Fact]
    public void A_request_that_names_no_page_is_the_first_page_of_the_size_the_list_gives()
    {
        var request = RequestOf("/api/things", "");

        var refusal = JsonApiPaging.Read(request, 100, 500, out var size, out var number);

        Assert.Null(refusal);
        Assert.Equal(100, size);
        Assert.Equal(1, number);
    }

    [Fact]
    public void A_request_that_names_a_page_gets_it()
    {
        var request = RequestOf("/api/things", "?page[size]=25&page[number]=3");

        var refusal = JsonApiPaging.Read(request, 100, 500, out var size, out var number);

        Assert.Null(refusal);
        Assert.Equal(25, size);
        Assert.Equal(3, number);
    }

    [Theory]
    [InlineData("?page[size]=0")]
    [InlineData("?page[size]=501")]
    [InlineData("?page[size]=many")]
    [InlineData("?page[number]=0")]
    [InlineData("?page[number]=-1")]
    [InlineData("?page[number]=first")]
    public async Task A_page_that_is_not_one_is_400_invalid_page_and_says_the_greatest_size_the_list_takes(string query)
    {
        var request = RequestOf("/api/things", query);

        var refusal = JsonApiPaging.Read(request, 100, 500, out _, out _);

        var (status, body) = await RunAsync(refusal!);
        Assert.Equal(400, status);
        var error = body.GetProperty("errors")[0];
        Assert.Equal("invalid-page", error.GetProperty("code").GetString());
        Assert.Equal("A page is a number of at least 1 and a size of 1 to 500.", error.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_list_answers_with_its_page_where_it_is_and_where_the_next_one_is()
    {
        var request = RequestOf("/api/things", "?filter=eventId eq 1&page[size]=2&page[number]=2");

        var page = JsonApiResults.Page(
            "things",
            [("a", new { name = "A" }), ("b", new { name = "B" })],
            request,
            size: 2,
            number: 2,
            hasNext: true
        );

        var (status, body) = await RunAsync(page);
        Assert.Equal(200, status);
        Assert.Equal(2, body.GetProperty("meta").GetProperty("page").GetProperty("size").GetInt32());
        Assert.Equal(2, body.GetProperty("meta").GetProperty("page").GetProperty("number").GetInt32());
        var links = body.GetProperty("links");
        Assert.Equal("/api/things?filter=eventId eq 1&page[size]=2&page[number]=2", links.GetProperty("self").GetString());
        Assert.Equal(
            "/api/things?filter=eventId%20eq%201&page%5Bsize%5D=2&page%5Bnumber%5D=3",
            links.GetProperty("next").GetString()
        );
        var data = body.GetProperty("data");
        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal("things", data[0].GetProperty("type").GetString());
        Assert.Equal("a", data[0].GetProperty("id").GetString());
        Assert.Equal("A", data[0].GetProperty("attributes").GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_last_page_has_no_next_link()
    {
        var request = RequestOf("/api/things", "?page[number]=3");

        var page = JsonApiResults.Page("things", [("a", new { name = "A" })], request, size: 100, number: 3, hasNext: false);

        var (_, body) = await RunAsync(page);
        Assert.False(body.GetProperty("links").TryGetProperty("next", out _));
    }

    [Fact]
    public async Task A_list_that_is_empty_is_200_with_an_empty_list()
    {
        var request = RequestOf("/api/things", "");

        var page = JsonApiResults.Page("things", [], request, size: 100, number: 1, hasNext: false);

        var (status, body) = await RunAsync(page);
        Assert.Equal(200, status);
        Assert.Equal(0, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task A_named_list_links_each_item_to_where_it_is_canonically_and_may_give_it_a_meta()
    {
        var request = RequestOf("/api/things/live", "");

        var page = JsonApiResults.Page(
            "things",
            [("a", new { name = "A" })],
            request,
            size: 100,
            number: 1,
            hasNext: false,
            itemLink: id => $"/api/things/{id}",
            itemMeta: id => new { version = 3 }
        );

        var (_, body) = await RunAsync(page);
        var item = body.GetProperty("data")[0];
        Assert.Equal("/api/things/a", item.GetProperty("links").GetProperty("self").GetString());
        Assert.Equal(3, item.GetProperty("meta").GetProperty("version").GetInt32());
    }

    static HttpRequest RequestOf(string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    static async Task<(int Status, JsonElement Body)> RunAsync(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body));
    }
}
