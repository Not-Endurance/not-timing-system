using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Not.Application.RPC;
using Not.Domain.Exceptions;
using NoTiming.Ui.Storage.Core.Repositories;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Events as the Ui reaches them (#628, ADR-0007, ADR-0008): the repository of the Ui over the real Api, on the JSON:API
/// variant of the client's repository. The Live Events and the Historic Events are the two named collections, read by
/// anybody; an Event is started from its Setup, changed and reset by its Main Operator, and what the Api refuses comes back
/// with its code. Whether an Event is Live is told by the clock of the Api, and nothing is stored or asked of a flag.
/// </summary>
public sealed class EventInformationRepositoryTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = DateTimeOffset.UtcNow;

    readonly MongoFixture _mongo;

    public EventInformationRepositoryTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Live_and_the_Historic_Events_are_read_through_the_named_collections_by_anybody_and_the_Historic_ones_last_day_first()
    {
        await using var api = NewApi();
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, NOW);
        var older = Guid.NewGuid();
        await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null, "Older", older);
        await EventSeed.StartAsync(_mongo.ConnectionString, older, tenant, null, NOW.AddDays(-5));
        var newer = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, null, NOW);
        var events = new EventInformationApiRepository(
            JsonApiClients.Of(api, null, out var requests),
            new SocketOf(null)
        );

        var liveEvents = (await events.ReadLive()).ToList();
        var historicEvents = (await events.ReadHistoric()).ToList();

        Assert.Contains(liveEvents, x => x.Id == live);
        Assert.DoesNotContain(liveEvents, x => x.Id is var id && (id == older || id == newer));
        Assert.Contains(historicEvents, x => x.Id == older);
        Assert.DoesNotContain(historicEvents, x => x.Id == live);
        var order = historicEvents.Select(x => x.Id).ToList();
        Assert.True(order.IndexOf(newer) < order.IndexOf(older));
        Assert.Equal(tenant, liveEvents.Single(x => x.Id == live).TenantId);
        Assert.Contains(requests.Asked, x => x.Contains("/api/events/live?"));
        Assert.Contains(requests.Asked, x => x.Contains("/api/events/historic?") && x.Contains("sort=-endDay"));
        Assert.Null(events.LastError);
    }

    [Fact]
    public async Task The_Main_Operator_starts_an_Event_from_its_Setup_and_it_is_one_of_the_Live_Events()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Started By The Ui");
        var events = new EventInformationApiRepository(
            JsonApiClients.Of(api, mainOperator, out var requests),
            new SocketOf(null)
        );

        var started = await events.Start(id);

        Assert.Equal(id, started.Id);
        Assert.Equal("Started By The Ui", started.Name);
        Assert.Equal(tenant, started.TenantId);
        Assert.Contains(await events.ReadLive(), x => x.Id == id);
        Assert.Contains($"POST /api/events", requests.Asked);
        Assert.Null(events.LastError);
    }

    [Fact]
    public async Task What_the_Api_refuses_when_an_Event_starts_is_thrown_with_what_it_says_and_its_code_is_kept()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var empty = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id, "Empty");
        var full = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var asMainOperator = new EventInformationApiRepository(
            JsonApiClients.Of(api, mainOperator, out _),
            new SocketOf(null)
        );
        var asMember = new EventInformationApiRepository(JsonApiClients.Of(api, member, out _), new SocketOf(null));

        var invalid = await Assert.ThrowsAsync<DomainException>(() => asMainOperator.Start(empty));
        var invalidCode = asMainOperator.LastError?.Code;
        var refused = await Assert.ThrowsAsync<DomainException>(() => asMember.Start(full));

        Assert.False(string.IsNullOrWhiteSpace(invalid.Message));
        Assert.Equal("invalid-setup", invalidCode);
        Assert.Equal("not-main-operator", asMember.LastError?.Code);
        Assert.False(string.IsNullOrWhiteSpace(refused.Message));
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, empty));
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, full));
    }

    [Fact]
    public async Task An_answer_to_a_start_that_holds_no_Event_is_not_taken_for_one()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var events = new EventInformationApiRepository(
            JsonApiClients.Of(api, mainOperator, out var requests),
            new SocketOf(null)
        );
        requests.Answer = _ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"data":null}""", Encoding.UTF8, "application/vnd.api+json"),
        };

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => events.Start(Guid.NewGuid()));

        Assert.Contains("no event payload", thrown.Message);
    }

    [Fact]
    public async Task A_Live_Event_is_changed_and_what_the_Api_keeps_is_not_sent_back_with_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var events = new EventInformationApiRepository(JsonApiClients.Of(api, mainOperator, out _), new SocketOf(null));
        var read = (await events.Read(id))!;
        var changed = new EventInformation(
            read.Country,
            "Renamed By The Ui",
            "Plovdiv",
            read.EventSpan,
            "FEI9",
            read.Id
        );

        await events.Update(changed);

        Assert.Null(events.LastError);
        var stored = (await EventSeed.CoreOfAsync(_mongo.ConnectionString, id))!;
        Assert.Equal("Renamed By The Ui", stored["Name"].AsString);
        Assert.Equal("Plovdiv", stored["Location"].AsString);
        Assert.Equal("FEI9", stored["FeiShowId"].AsString);
        Assert.Equal(mainOperator.Id, stored["MainOperatorId"].AsGuid);
    }

    [Fact]
    public async Task The_Event_the_Ui_has_selected_is_reset_by_its_Main_Operator_and_is_no_longer_Live()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.FullSetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id);
        var starter = new EventInformationApiRepository(
            JsonApiClients.Of(api, mainOperator, out _),
            new SocketOf(null)
        );
        var started = await starter.Start(id);
        var events = new EventInformationApiRepository(
            JsonApiClients.Of(api, mainOperator, out _),
            new SocketOf(started)
        );

        await events.Reset();

        Assert.Null(events.LastError);
        Assert.Null(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
        Assert.DoesNotContain(await events.ReadLive(), x => x.Id == id);
        Assert.NotNull(await EventSeed.SetupOfAsync(_mongo.ConnectionString, id));
    }

    [Fact]
    public async Task Resetting_with_no_Event_selected_asks_nothing_of_the_Api_and_changes_nothing()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var id = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, NOW);
        var events = new EventInformationApiRepository(
            JsonApiClients.Of(api, mainOperator, out var requests),
            new SocketOf(null)
        );

        await events.Reset();

        Assert.Empty(requests.Asked);
        Assert.Null(events.LastError);
        Assert.NotNull(await EventSeed.CoreOfAsync(_mongo.ConnectionString, id));
    }

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString, time: new FakeTimeProvider(NOW));
    }

    /// <summary>The Event the app is connected to, as the repository asks for it when it resets one.</summary>
    sealed class SocketOf : INtsSocketContext
    {
        public SocketOf(EventInformation? selected)
        {
            Event = selected;
        }

        public bool IsConnected => Event != null;
        public SocketConnectionStatus Status =>
            IsConnected ? SocketConnectionStatus.Connected : SocketConnectionStatus.Disconnected;
        public EventInformation? Event { get; }
    }
}
