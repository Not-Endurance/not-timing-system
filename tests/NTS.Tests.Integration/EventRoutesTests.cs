using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Events as the public sees them (#628, ADR-0007, ADR-0012): the <c>events</c> resource of every Tenant, read by
/// anybody. An Event is Live until the end of its last day and Historic from then on, which the clock of the host decides
/// and which the resource says in <c>isLive</c>; the named collections <c>/api/events/live</c> and
/// <c>/api/events/historic</c> split the Events by it. An Event that has not started is a Setup, and is not an Event here.
/// </summary>
public sealed class EventRoutesTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public EventRoutesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_named_collections_split_the_started_Events_of_every_Tenant_by_the_clock_and_isLive_agrees()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);
        var a = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var b = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var liveA = await EventSeed.LiveAsync(_mongo.ConnectionString, a, null, NOW);
        var lastSecond = Guid.NewGuid();
        await EventSeed.SetupAsync(_mongo.ConnectionString, b, null, "Last second", lastSecond);
        await EventSeed.StartAsync(_mongo.ConnectionString, lastSecond, b, null, NOW.AddSeconds(1));
        var historicA = await EventSeed.HistoricAsync(_mongo.ConnectionString, a, null, NOW);
        var endingNow = Guid.NewGuid();
        await EventSeed.SetupAsync(_mongo.ConnectionString, b, null, "Ending now", endingNow);
        await EventSeed.StartAsync(_mongo.ConnectionString, endingNow, b, null, NOW);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, a, null);

        var live = await IdsAsync(client, "/api/events/live?page[size]=500");
        var historic = await IdsAsync(client, "/api/events/historic?page[size]=500");
        var all = await ListAsync(client, "/api/events?page[size]=500");

        Assert.Contains(liveA, live);
        Assert.Contains(lastSecond, live);
        Assert.DoesNotContain(historicA, live);
        Assert.DoesNotContain(endingNow, live);
        Assert.Contains(historicA, historic);
        Assert.Contains(endingNow, historic);
        Assert.DoesNotContain(liveA, historic);
        Assert.DoesNotContain(lastSecond, historic);
        Assert.DoesNotContain(unstarted, live.Concat(historic));
        foreach (var @event in all.GetProperty("data").EnumerateArray())
        {
            var id = Guid.Parse(@event.GetProperty("id").GetString()!);
            Assert.Equal(live.Contains(id), @event.GetProperty("attributes").GetProperty("isLive").GetBoolean());
            Assert.Equal($"/api/events/{id}", @event.GetProperty("links").GetProperty("self").GetString());
        }
    }

    [Fact]
    public async Task The_clock_moves_an_Event_from_the_live_list_to_the_historic_one_at_its_end()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var id = Guid.NewGuid();
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null, "Moving", id);
        await EventSeed.StartAsync(_mongo.ConnectionString, id, tenant, null, NOW.AddMinutes(10));

        var before = await IdsAsync(client, "/api/events/live?page[size]=500");
        time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        var lastSecond = await IdsAsync(client, "/api/events/live?page[size]=500");
        time.Advance(TimeSpan.FromSeconds(1));
        var live = await IdsAsync(client, "/api/events/live?page[size]=500");
        var historic = await IdsAsync(client, "/api/events/historic?page[size]=500");
        var read = await ApiSessions.ReadJsonAsync(await client.GetAsync($"/api/events/{id}"));

        Assert.Contains(id, before);
        Assert.Contains(id, lastSecond);
        Assert.DoesNotContain(id, live);
        Assert.Contains(id, historic);
        Assert.False(read.GetProperty("data").GetProperty("attributes").GetProperty("isLive").GetBoolean());
    }

    [Fact]
    public async Task Anybody_reads_an_Event_by_its_id_in_whatever_Tenant_it_is_and_an_Event_that_has_not_started_is_not_there()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, NOW);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var response = await client.GetAsync($"/api/events/{live}");
        var notStarted = await client.GetAsync($"/api/events/{unstarted}");
        var missing = await client.GetAsync($"/api/events/{Guid.NewGuid()}");
        var notAnId = await client.GetAsync("/api/events/spring-ride");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.api+json", response.Content.Headers.ContentType?.MediaType);
        var resource = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal("events", resource.GetProperty("type").GetString());
        Assert.Equal(live.ToString(), resource.GetProperty("id").GetString());
        Assert.Equal(tenant, resource.GetProperty("attributes").GetProperty("tenantId").GetString());
        Assert.True(resource.GetProperty("attributes").GetProperty("isLive").GetBoolean());
        foreach (var refused in new[] { notStarted, missing, notAnId })
        {
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            Assert.Equal("not-found", await ErrorCodeAsync(refused));
        }
    }

    [Fact]
    public async Task What_an_Event_shows_is_what_the_public_views_need_and_leaves_out_who_runs_it()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var id = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, Guid.NewGuid(), NOW);

        var attributes = (await ApiSessions.ReadJsonAsync(await client.GetAsync($"/api/events/{id}")))
            .GetProperty("data")
            .GetProperty("attributes");

        var shown = attributes.EnumerateObject().Select(x => x.Name).ToList();
        Assert.All(
            ["country", "endDay", "isLive", "location", "name", "startDay", "tenantId"],
            x => Assert.Contains(x, shown)
        );
        Assert.All(["id", "mainOperatorId", "isDeleted", "deletedVersion"], x => Assert.DoesNotContain(x, shown));
        Assert.Equal("BG", attributes.GetProperty("country").GetProperty("isoCode").GetString());
    }

    [Fact]
    public async Task The_lists_are_filtered_sorted_and_cut_into_pages_like_every_list()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        foreach (var (id, days) in new[] { (first, 1), (second, 2), (third, 3) })
        {
            await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null, $"Ride {days}", id);
            await EventSeed.StartAsync(_mongo.ConnectionString, id, tenant, null, NOW.AddDays(days));
        }

        await EventSeed.LiveAsync(_mongo.ConnectionString, other, null, NOW);

        var filter = Uri.EscapeDataString($"tenantId eq '{tenant}'");
        var byEnd = await ListAsync(client, $"/api/events/live?filter={filter}&sort=-endDay&page[size]=2");
        var rest = await ListAsync(
            client,
            $"/api/events/live?filter={filter}&sort=-endDay&page[size]=2&page[number]=2"
        );
        var after = NOW.AddDays(1.5).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var later = await ListAsync(
            client,
            $"/api/events/live?filter={Uri.EscapeDataString($"tenantId eq '{tenant}' and endDay gt {after}")}&sort=endDay"
        );

        Assert.Equal([third, second], Ids(byEnd));
        Assert.StartsWith("/api/events/live?", byEnd.GetProperty("links").GetProperty("next").GetString());
        Assert.Equal([first], Ids(rest));
        Assert.False(rest.GetProperty("links").TryGetProperty("next", out _));
        Assert.Equal([second, third], Ids(later));
    }

    [Theory]
    [InlineData("filter", "year(endDay) eq 2026", "invalid-filter")]
    [InlineData("filter", "isLive eq true", "invalid-filter")]
    [InlineData("filter", "mainOperatorId ne null", "invalid-filter")]
    [InlineData("filter", "name eq", "invalid-filter")]
    [InlineData("sort", "nothing", "invalid-sort")]
    [InlineData("page[size]", "0", "invalid-page")]
    public async Task A_filter_the_data_cannot_run_or_does_not_know_is_refused_and_is_not_a_server_error(
        string parameter,
        string value,
        string code
    )
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync($"/api/events/live?{parameter}={Uri.EscapeDataString(value)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData("include=anything")]
    [InlineData("fields[events]=name")]
    [InlineData("allTenants=true")]
    public async Task Any_other_parameter_is_refused_and_none_changes_whose_Events_are_read(string query)
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);

        var response = await client.GetAsync("/api/events?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("unsupported-parameter", await ErrorCodeAsync(response));
    }

    ApiFactory NewApi(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(NOW);
        return new ApiFactory(_mongo.ConnectionString, time: time);
    }

    static async Task<JsonElement> ListAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{path} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );
        return await ApiSessions.ReadJsonAsync(response);
    }

    static async Task<HashSet<Guid>> IdsAsync(HttpClient client, string path)
    {
        return [.. Ids(await ListAsync(client, path))];
    }

    static IEnumerable<Guid> Ids(JsonElement list)
    {
        return list.GetProperty("data").EnumerateArray().Select(x => Guid.Parse(x.GetProperty("id").GetString()!));
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }
}
