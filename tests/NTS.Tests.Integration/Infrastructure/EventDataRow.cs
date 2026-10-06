using System.Text.Json;
using NTS.Contracts.Core.Models;
using NTS.Tests.Integration.Drivers;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A row of one of the four families of what an Event keeps, as the tests of what the Api does with them need it: what a
/// client sends to make it, what it sends to change it, how it is seeded and where it is stored. A Participation counts its
/// writes, so the change of one is sent with the version it was based on.
/// </summary>
internal sealed class EventDataRow
{
    public static EventDataRow Of(string route, Guid eventId)
    {
        var participation = IntegrationPayloadFactory.TwoPhaseParticipation(eventId, 1, Guid.NewGuid());
        switch (route)
        {
            case "participations":
                return new EventDataRow(
                    route,
                    "event_participations",
                    participation.Id,
                    JsonApiAttributes.Of(ParticipationModel.MapFrom(participation)),
                    new { eliminated = new { code = "WD" } },
                    isCounted: true,
                    (connection, tenant) => EventSeed.ParticipationAsync(connection, tenant, participation)
                );
            case "rankings":
                var ranking = IntegrationPayloadFactory.Ranking(eventId, [participation], Guid.NewGuid());
                return new EventDataRow(
                    route,
                    "event_rankings",
                    ranking.Id,
                    JsonApiAttributes.Of(RankingModel.From(ranking)),
                    new { name = "Renamed" },
                    isCounted: false,
                    (connection, tenant) => EventSeed.RankingAsync(connection, tenant, ranking)
                );
            case "officials":
                var official = IntegrationPayloadFactory.Official(eventId, null, Guid.NewGuid());
                return new EventDataRow(
                    route,
                    "event_officials",
                    official.Id,
                    JsonApiAttributes.Of(OfficialModel.MapFrom(official), "userId"),
                    new { nameEnglish = "Renamed" },
                    isCounted: false,
                    (connection, tenant) => EventSeed.OfficialAsync(connection, tenant, official)
                );
            default:
                var handout = IntegrationPayloadFactory.Handout(participation, Guid.NewGuid());
                return new EventDataRow(
                    route,
                    "event_handouts",
                    handout.Id,
                    JsonApiAttributes.Of(HandoutModel.From(handout)),
                    new { participationId = Guid.NewGuid() },
                    isCounted: false,
                    (connection, tenant) => EventSeed.HandoutAsync(connection, tenant, handout)
                );
        }
    }

    readonly Func<string, string, Task> _seed;

    EventDataRow(
        string route,
        string collection,
        Guid id,
        Dictionary<string, JsonElement> attributes,
        object change,
        bool isCounted,
        Func<string, string, Task> seed
    )
    {
        Route = route;
        Collection = collection;
        Id = id;
        Attributes = attributes;
        Change = change;
        IsCounted = isCounted;
        _seed = seed;
    }

    public string Route { get; }
    public string Collection { get; }
    public Guid Id { get; }

    /// <summary>What a client sends to make the row.</summary>
    public Dictionary<string, JsonElement> Attributes { get; }

    /// <summary>What a client sends to change a member of the row.</summary>
    public object Change { get; }

    /// <summary>Whether the row counts its writes, so that a change of it names the version it was based on.</summary>
    public bool IsCounted { get; }

    public Task SeedAsync(string mongoConnectionString, string tenant)
    {
        return _seed(mongoConnectionString, tenant);
    }

    /// <summary>The <c>meta</c> of a change of the row, which names the version when the row counts its writes.</summary>
    public object? MetaOf(int version)
    {
        return IsCounted ? new { version } : null;
    }
}
