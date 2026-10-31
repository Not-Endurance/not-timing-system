using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Not.Application.HTTP;
using NTS.Tests.Unit.Harness;

namespace NTS.Tests.Unit.Not.Application.HTTP;

/// <summary>
/// What the client sends and what it reads, checked at the transport (ADR-0017): the verbs, the document of a resource, the
/// headers of a write, and an answer that refused a request as a value rather than as an exception.
/// </summary>
public sealed class JsonApiClientTests
{
    static readonly Guid AN_ATHLETE = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
    static readonly Guid AN_EVENT = Guid.Parse("9b2f1c64-7d0e-4a5b-8c31-5e6f7a8b9c0d");

    [Fact]
    public async Task A_list_is_asked_with_its_filter_sort_and_page()
    {
        var transport = new FakeJsonApi();

        var query = new ListQuery(filter: $"eventId eq {AN_EVENT}", sort: "-name", pageSize: 500, pageNumber: 2);

        await Client(transport).Get("athletes", query);

        Assert.Equal(
            $"/api/athletes?filter=eventId eq {AN_EVENT}&sort=-name&page[size]=500&page[number]=2",
            transport.Requests.Single().Url
        );
    }

    [Fact]
    public async Task A_single_resource_is_asked_by_its_id()
    {
        var transport = new FakeJsonApi();

        await Client(transport).GetOne("athletes", AN_ATHLETE);

        Assert.Equal(HttpMethod.Get, transport.Requests.Single().Method);
        Assert.Equal($"/api/athletes/{AN_ATHLETE}", transport.Requests.Single().Url);
    }

    [Fact]
    public async Task A_post_sends_the_model_as_a_resource_whose_id_is_its_id_and_not_an_attribute()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Post("athletes", new ResourceSample(AN_ATHLETE, "Ana", null));

        var document = JsonDocument.Parse(transport.Requests.Single().Body!).RootElement.GetProperty("data");
        Assert.Equal("athletes", document.GetProperty("type").GetString());
        Assert.Equal(AN_ATHLETE.ToString(), document.GetProperty("id").GetString());
        Assert.False(document.GetProperty("attributes").TryGetProperty("id", out _));
    }

    [Fact]
    public async Task A_member_that_is_null_is_written_as_null()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Post("athletes", new ResourceSample(AN_ATHLETE, "Ana", null));

        var document = JsonDocument.Parse(transport.Requests.Single().Body!).RootElement;
        var attributes = document.GetProperty("data").GetProperty("attributes");
        Assert.Equal(JsonValueKind.Null, attributes.GetProperty("note").ValueKind);
    }

    [Fact]
    public async Task A_model_with_no_id_is_sent_without_one()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Post("athletes", new ResourceSample(Guid.Empty, "Ana", null));

        var document = JsonDocument.Parse(transport.Requests.Single().Body!).RootElement.GetProperty("data");
        Assert.False(document.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task A_patch_sends_the_route_id_as_the_document_id_and_the_version_in_meta()
    {
        var transport = new FakeJsonApi();
        var meta = new System.Text.Json.Nodes.JsonObject { ["version"] = 3 };

        await Client(transport).Patch("athletes", AN_ATHLETE, new ResourceSample(AN_ATHLETE, "Ana", null), meta);

        var request = transport.Requests.Single();
        var document = JsonDocument.Parse(request.Body!).RootElement.GetProperty("data");
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal($"/api/athletes/{AN_ATHLETE}", request.Url);
        Assert.Equal(AN_ATHLETE.ToString(), document.GetProperty("id").GetString());
        Assert.Equal(3, document.GetProperty("meta").GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task A_delete_asks_for_the_row_and_sends_no_body()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Delete("athletes", AN_ATHLETE);

        var request = transport.Requests.Single();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/api/athletes/{AN_ATHLETE}", request.Url);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task Every_request_asks_for_the_JSON_API_media_type_and_a_write_carries_the_write_headers()
    {
        var transport = new FakeJsonApi();
        var client = Client(transport, writeHeaders: new() { ["X-Test-Write"] = "yes" });

        await client.GetOne("athletes", AN_ATHLETE);
        await client.Post("athletes", new ResourceSample(AN_ATHLETE, "Ana", null));

        var read = transport.Requests[0];
        var write = transport.Requests[1];
        Assert.Equal("application/vnd.api+json", read.Accept);
        Assert.Equal("application/vnd.api+json", write.Accept);
        Assert.False(read.Headers.ContainsKey("X-Test-Write"));
        Assert.Equal("yes", write.Headers["X-Test-Write"]);
    }

    [Fact]
    public async Task An_answer_that_refused_the_request_is_a_value_with_its_code_and_is_not_thrown()
    {
        var transport = new FakeJsonApi(_ => Json(HttpStatusCode.Conflict, Errors("id-taken", "The row is taken.")));

        var response = await Client(transport).Post("athletes", new ResourceSample(AN_ATHLETE, "Ana", null));

        Assert.False(response.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Equal("id-taken", response.Error!.Code);
    }

    [Fact]
    public async Task A_row_that_is_not_there_is_an_answer_and_not_an_error_of_the_transport()
    {
        var transport = new FakeJsonApi(_ =>
            Json(HttpStatusCode.NotFound, Errors("not-found", "There is no such row.")));

        var response = await Client(transport).GetOne("athletes", AN_ATHLETE);

        Assert.True(response.IsNotFound);
    }

    [Fact]
    public async Task The_headers_of_an_answer_are_kept_so_that_a_caller_can_read_retry_after()
    {
        var transport = new FakeJsonApi(_ =>
        {
            var response = Json(HttpStatusCode.TooManyRequests, Errors("too-many", "Wait a while."));
            response.Headers.Add("Retry-After", "30");
            return response;
        });

        var response = await Client(transport).GetOne("athletes", AN_ATHLETE);

        Assert.Equal("30", response.Headers["Retry-After"]);
    }

    [Fact]
    public async Task A_request_that_got_no_answer_throws_the_transport_exception()
    {
        var transport = new FakeJsonApi(_ => throw new HttpRequestException("The server did not answer."));

        await Assert.ThrowsAsync<HttpRequestException>(() => Client(transport).GetOne("athletes", AN_ATHLETE));
    }

    [Fact]
    public async Task A_document_is_written_with_the_options_the_settings_hold()
    {
        var transport = new FakeJsonApi();
        var settings = new JsonApiSettings { Url = "https://api.test/api" };
        settings.Json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNamingPolicy = null };

        await new JsonApiClient(new FakeHttpClientFactory(transport), Options.Create(settings))
            .Post("athletes", new ResourceSample(AN_ATHLETE, "Ana", null));

        var document = JsonDocument.Parse(transport.Requests.Single().Body!).RootElement;
        Assert.Equal("Ana", document.GetProperty("data").GetProperty("attributes").GetProperty("Name").GetString());
    }

    [Fact]
    public async Task A_change_at_a_path_sends_the_type_and_the_id_it_is_given()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Patch("me/profile", "profiles", "9f1c", new ResourceSample(AN_ATHLETE, "Ana", null));

        var request = transport.Requests.Single();
        var document = JsonDocument.Parse(request.Body!).RootElement.GetProperty("data");
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/api/me/profile", request.Url);
        Assert.Equal("profiles", document.GetProperty("type").GetString());
        Assert.Equal("9f1c", document.GetProperty("id").GetString());
    }

    [Fact]
    public async Task A_row_whose_id_is_text_keeps_the_text_of_its_id()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Patch("tenants/country-bg", "tenants", "country-bg", new ResourceSample(AN_ATHLETE, "Bg", null));

        var document = JsonDocument.Parse(transport.Requests.Single().Body!).RootElement.GetProperty("data");
        Assert.Equal("country-bg", document.GetProperty("id").GetString());
    }

    [Fact]
    public async Task An_action_is_posted_to_its_path_with_no_document()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Post($"athletes/{AN_ATHLETE}/link");

        var request = transport.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/athletes/{AN_ATHLETE}/link", request.Url);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task An_item_that_is_not_addressed_by_a_collection_is_deleted_at_its_path_with_no_document()
    {
        var transport = new FakeJsonApi();

        await Client(transport).Delete("sessions/current");

        var request = transport.Requests.Single();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/sessions/current", request.Url);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task A_list_is_read_a_page_at_a_time_until_the_Api_says_there_is_no_next_page()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var transport = new FakeJsonApi(request =>
            request.RequestUri!.ToString().Contains("page[number]=1")
                ? Page(new[] { first, second }, hasNext: true)
                : Page(new[] { third }, hasNext: false)
        );

        var rows = await Client(transport).GetAll("athletes", new ListQuery(pageSize: 2), maxPages: 5);

        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal(new[] { first, second, third }, rows.Select(x => Guid.Parse(x.GetProperty("id").GetString()!)));
    }

    [Fact]
    public async Task A_list_that_goes_on_beyond_the_pages_asked_for_is_refused_and_not_cut_short()
    {
        var transport = new FakeJsonApi(_ => Page(new[] { Guid.NewGuid() }, hasNext: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Client(transport).GetAll("athletes", new ListQuery(pageSize: 1), maxPages: 2)
        );
        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task A_page_that_is_refused_is_thrown_with_what_the_Api_said()
    {
        var transport = new FakeJsonApi(_ => Json(HttpStatusCode.Forbidden, Errors("not-yours", "Not for you.")));

        var refused = await Assert.ThrowsAsync<JsonApiException>(() =>
            Client(transport).GetAll("athletes", new ListQuery(pageSize: 2), maxPages: 2)
        );
        Assert.Equal("not-yours", refused.Response.Error!.Code);
    }

    [Fact]
    public async Task Several_resources_are_posted_to_an_action_as_one_document_of_many()
    {
        var transport = new FakeJsonApi();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await Client(transport)
            .PostMany(
                "snapshots/actions/send-group",
                new[]
                {
                    ("snapshots", first.ToString(), (object)new ResourceSample(first, "First", null)),
                    ("snapshots", second.ToString(), (object)new ResourceSample(second, "Second", null)),
                }
            );

        var request = transport.Requests.Single();
        var data = JsonDocument.Parse(request.Body!).RootElement.GetProperty("data");
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/snapshots/actions/send-group", request.Url);
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.Equal(new[] { first.ToString(), second.ToString() }, data.EnumerateArray().Select(x => x.GetProperty("id").GetString()!));
        Assert.All(data.EnumerateArray(), x => Assert.Equal("snapshots", x.GetProperty("type").GetString()));
    }

    static JsonApiClient Client(FakeJsonApi transport, Dictionary<string, string>? writeHeaders = null)
    {
        var settings = new JsonApiSettings { Url = "https://api.test/api" };
        foreach (var header in writeHeaders ?? [])
        {
            settings.WriteHeaders[header.Key] = header.Value;
        }

        return new JsonApiClient(new FakeHttpClientFactory(transport), Options.Create(settings));
    }

    /// <summary>A page of athletes, with the link to the next one when another page follows.</summary>
    static HttpResponseMessage Page(IEnumerable<Guid> ids, bool hasNext)
    {
        var rows = string.Join(
            ",",
            ids.Select(id => "{\"type\":\"athletes\",\"id\":\"" + id + "\",\"attributes\":{\"name\":\"Ana\"}}")
        );
        var links = hasNext ? ",\"links\":{\"next\":\"/api/athletes?page[number]=2\"}" : "";
        return Json(HttpStatusCode.OK, "{\"data\":[" + rows + "]" + links + "}");
    }

    static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/vnd.api+json"),
        };
    }

    static string Errors(string code, string title)
    {
        return $$"""{"errors":[{"code":"{{code}}","title":"{{title}}"}]}""";
    }

}
