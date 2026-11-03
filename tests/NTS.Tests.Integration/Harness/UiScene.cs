using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Reference;
using NoTiming.Ui.Storage.REST;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Harness.Drivers;

namespace NTS.Tests.Integration.Harness;

/// <summary>A Live Event with three Participations that have started an hour ago, and the people around it.</summary>
internal sealed class UiScene
{
    /// <summary>A Live Event with three Participations that have started an hour ago, and the people around it.</summary>
    public static async Task<UiScene> CreateAsync(ApiHostFixture host)
    {
        var mongo = host.MongoConnectionString;
        ApiMongo.Configure();
        var tenant = await TenancySeed.TenantAsync(mongo);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant);
        var eventId = await EventSeed.LiveAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow);
        var started = DateTimeOffset.Now.AddHours(-1);
        var ids = new List<Guid>();
        for (var number = 1; number <= 3; number++)
        {
            var participation = IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                number,
                Guid.NewGuid(),
                startTime: started
            );
            await EventSeed.ParticipationAsync(mongo, tenant, participation);
            ids.Add(participation.Id);
        }

        var client = host.Api.CreateClient();
        var official = await TenancySeed.SignedInAsync(host.Api, client, mongo, tenant, name: "Ana Petrova");
        await EventSeed.GrantAsync(mongo, tenant, eventId, "Official", "Steward", official.Email, official.Id);
        return new UiScene(host, tenant, eventId, official, ids);
    }

    readonly ApiHostFixture _host;
    readonly IReadOnlyList<Guid> _participations;

    public UiScene(
        ApiHostFixture host,
        string tenant,
        Guid eventId,
        TenancySeed.Person official,
        IReadOnlyList<Guid> participations
    )
    {
        _host = host;
        _participations = participations;
        Tenant = tenant;
        EventId = eventId;
        Official = official;
    }

    public string Tenant { get; }
    public Guid EventId { get; }

    /// <summary>The Official who has been granted the Event, with a session.</summary>
    public TenancySeed.Person Official { get; }

    /// <summary>A person who is signed up and has been granted nothing.</summary>
    public async Task<TenancySeed.Person> RegisteredAsync()
    {
        using var client = _host.Api.CreateClient();
        return await TenancySeed.SignedInAsync(
            _host.Api,
            client,
            _host.MongoConnectionString,
            Tenant,
            name: "Boris Ivanov"
        );
    }

    /// <summary>The Console Operator takes the grant of the person on the Event away.</summary>
    public async Task RemoveGrantAsync(string email)
    {
        await EventSeed.Grants(_host.MongoConnectionString).DeleteManyAsync(new BsonDocument("Email", email));
    }

    /// <summary>The Official sends the Snapshots of the numbers as of now, as a program does.</summary>
    public async Task SendAsync(SnapshotType type, params int[] numbers)
    {
        var publisher = new SnapshotApiPublisher(JsonApiClients.Over(_host.BaseAddress, Official, out _));
        var group = new SnapshotGroup(
            numbers.Select(number => new Snapshot(
                number,
                "Integration Rider",
                null,
                new Timestamp(DateTimeOffset.Now)
            )),
            type
        );
        var receipts = await publisher.PublishSnapshotsAsync(EventId, group);
        Assert.All(receipts, receipt => Assert.True(receipt.IsRecorded, receipt.ErrorMessage));
    }

    /// <summary>The time events the Participation of the number keeps in its Phase, as the Api stored them.</summary>
    public async Task<List<BsonDocument>> EventsOfAsync(int number)
    {
        var stored = (
            await RegistrySeed.StoredAsync(
                _host.MongoConnectionString,
                "event_participations",
                _participations[number - 1]
            )
        )!;
        return [.. stored["Phases"][0]["Events"].AsBsonArray.Select(x => x.AsBsonDocument)];
    }
}
