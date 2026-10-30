using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>An Athlete or a Horse as a ride names it: the id of its row of the registry, and the names it was entered with.</summary>
internal sealed class Participant
{
    public Participant(Guid id, string name, string? nameEnglish = null)
    {
        Id = id;
        Name = name;
        NameEnglish = nameEnglish;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? NameEnglish { get; }
}

/// <summary>
/// What the history of a Participant reads (#638): Events that have started and ended or are still on, with the
/// Participations of made-up rides and the Rankings that count them, stored the way the Api keeps them. Every ride names the rows
/// of the registry by id, as the copies of a Participation do.
/// </summary>
internal static class HistorySeed
{
    /// <summary>An Event that has started, in the Tenant, from the first instant of its first day to the last of its last.</summary>
    public static async Task<Guid> EventAsync(
        string mongoConnectionString,
        string tenant,
        string name,
        string location,
        DateTimeOffset firstDay,
        DateTimeOffset lastDay
    )
    {
        var id = Guid.NewGuid();
        await EventSeed
            .Cores(mongoConnectionString)
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", EventSeed.Binary(id) },
                    { "TenantId", tenant },
                    { "Name", name },
                    { "Location", location },
                    { "Country", RegistrySeed.CountryOf("Bulgaria", "BG") },
                    { "StartDay", new BsonDateTime(firstDay.UtcDateTime) },
                    { "EndDay", new BsonDateTime(lastDay.UtcDateTime) },
                }
            );
        return id;
    }

    /// <summary>An Event that ended a day before now, on a day (the start of it a day before the end) that the test chooses by the days back.</summary>
    public static async Task<Guid> HistoricAsync(
        string mongoConnectionString,
        string tenant,
        string name,
        string location,
        int daysAgo
    )
    {
        var end = DateTimeOffset.UtcNow.Date.AddDays(-daysAgo).AddHours(23);
        return await EventAsync(mongoConnectionString, tenant, name, location, end.AddHours(-23), end);
    }

    /// <summary>An Event that has started and is Live for another day.</summary>
    public static async Task<Guid> LiveAsync(string mongoConnectionString, string tenant, string name, string location)
    {
        var now = DateTimeOffset.UtcNow;
        return await EventAsync(mongoConnectionString, tenant, name, location, now.AddHours(-2), now.AddDays(1));
    }

    /// <summary>
    /// A ride that was ridden to the end of its one Phase, arriving at the time given (an hour before now by default) and presented
    /// five minutes after, or eliminated when the elimination is given.
    /// </summary>
    public static Participation Ride(
        Guid eventId,
        int number,
        Participant athlete,
        Participant horse,
        string competition = "CEI 1*",
        CompetitionRuleset ruleset = CompetitionRuleset.FEI,
        ParticipationCategory category = ParticipationCategory.Senior,
        Eliminated? eliminated = null,
        DateTimeOffset? arrive = null,
        Guid? id = null
    )
    {
        var country = new Country(Guid.NewGuid(), "Bulgaria", "BG", "BUL", "bg-BG");
        var combination = new Combination(
            number,
            new Athlete(athlete.Name, athlete.NameEnglish, country, null, null, athlete.Id),
            new Horse(horse.Name, horse.NameEnglish, null, horse.Id),
            club: null,
            distance: "40",
            minAverageSpeed: null,
            maxAverageSpeed: null,
            id: Guid.NewGuid()
        );
        var at = arrive ?? DateTimeOffset.UtcNow.AddHours(-1);
        var phase = new Phase(
            gate: "GATE1/40",
            length: 40,
            maxRecovery: 40,
            rest: null,
            ruleset: ruleset,
            isFinal: true,
            compulsoryThresholdSpan: null,
            startTime: new Timestamp(at.AddHours(-3)),
            arriveTime: new Timestamp(at),
            presentTime: new Timestamp(at.AddMinutes(5)),
            representTime: null,
            isRepresentationRequested: false,
            isRequiredInspectionRequested: false,
            isRequiredInspectionCompulsory: false,
            id: Guid.NewGuid()
        );
        return new Participation(
            category,
            new Competition(competition, ruleset),
            combination,
            new PhaseCollection([phase]),
            eliminated,
            eventId,
            id ?? Guid.NewGuid()
        );
    }

    /// <summary>A Ranking of the Event that counts the rides given as their entries, in the order given, with the ranks they hold.</summary>
    public static Ranking Ranking(
        Guid eventId,
        string name,
        CompetitionRuleset ruleset,
        ParticipationCategory category,
        params RankingEntry[] entries
    )
    {
        return RankingWithId(Guid.NewGuid(), eventId, name, ruleset, category, entries);
    }

    /// <summary>The same Ranking with the id given, for a test that depends on how the ids are ordered.</summary>
    public static Ranking RankingWithId(
        Guid id,
        Guid eventId,
        string name,
        CompetitionRuleset ruleset,
        ParticipationCategory category,
        params RankingEntry[] entries
    )
    {
        return new Ranking(name, ruleset, category, null, null, null, null, null, entries, eventId, id);
    }

    /// <summary>
    /// A ride of the Event, stored with the Ranking of its own Competition (Senior, FEI) that counts it: ranked with the rank
    /// given, or not ranked, or eliminated.
    /// </summary>
    public static async Task<Participation> RideAsync(
        string mongoConnectionString,
        string tenant,
        Guid eventId,
        int number,
        Participant athlete,
        Participant horse,
        int rank = 1,
        bool notRanked = false,
        Eliminated? eliminated = null,
        string competition = "CEI 1*"
    )
    {
        var ride = Ride(eventId, number, athlete, horse, competition, eliminated: eliminated);
        await EventSeed.ParticipationAsync(mongoConnectionString, tenant, ride);
        await EventSeed.RankingAsync(
            mongoConnectionString,
            tenant,
            Ranking(
                eventId,
                competition,
                CompetitionRuleset.FEI,
                ParticipationCategory.Senior,
                new RankingEntry(ride.Id, notRanked, notRanked ? null : rank)
            )
        );
        return ride;
    }

    /// <summary>Another Ranking of the Event, which counts the ride too, with the rank given.</summary>
    public static async Task CountAlsoAsync(
        string mongoConnectionString,
        string tenant,
        Guid eventId,
        string name,
        Participation ride,
        int rank,
        ParticipationCategory category = ParticipationCategory.Senior
    )
    {
        await EventSeed.RankingAsync(
            mongoConnectionString,
            tenant,
            Ranking(eventId, name, CompetitionRuleset.Regional, category, new RankingEntry(ride.Id, false, rank))
        );
    }

    /// <summary>
    /// What the account has linked itself to: the Athlete that is the person, and the Horses they added. The links are one
    /// document for the account, whose id is the account's.
    /// </summary>
    public static async Task LinkAsync(string mongoConnectionString, Guid account, Guid? athlete, params Guid[] horses)
    {
        var links = new BsonDocument
        {
            { "_id", EventSeed.Binary(account) },
            { "HorseIds", new BsonArray(horses.Select(EventSeed.Binary)) },
        };
        if (athlete != null)
        {
            links["AthleteId"] = EventSeed.Binary(athlete.Value);
        }

        await RegistrySeed.Collection(mongoConnectionString, "user_links").InsertOneAsync(links);
    }

    /// <summary>
    /// Letters that a Latin name and a Cyrillic one write the same, as a text for a name to hold: a Participant whose name has the
    /// one is found by the other. The database is shared by every test, and a search reads all of it, so each test has a text of
    /// its own.
    /// </summary>
    public static (string Latin, string Cyrillic) NameMarker()
    {
        const string LATIN = "bdlmnprt";
        const string CYRILLIC = "бдлмнпрт";
        var letters = Enumerable.Range(0, 8).Select(_ => Random.Shared.Next(LATIN.Length)).ToList();
        return (string.Concat(letters.Select(x => LATIN[x])), string.Concat(letters.Select(x => CYRILLIC[x])));
    }

    /// <summary>Takes the stored Participation away.</summary>
    public static async Task DeleteAsync(string mongoConnectionString, Guid participationId)
    {
        await Participations(mongoConnectionString)
            .DeleteOneAsync(new BsonDocument("_id", EventSeed.Binary(participationId)));
    }

    /// <summary>Marks the stored Event as one that was deleted before the Api stopped keeping the flag.</summary>
    public static async Task SoftDeleteEventAsync(string mongoConnectionString, Guid eventId)
    {
        await EventSeed
            .Cores(mongoConnectionString)
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(eventId)),
                new BsonDocument("$set", new BsonDocument("IsDeleted", true))
            );
    }

    /// <summary>Marks the stored Participation as one that was deleted before the Api stopped keeping the flag.</summary>
    public static async Task SoftDeleteAsync(string mongoConnectionString, Guid participationId)
    {
        await Participations(mongoConnectionString)
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(participationId)),
                new BsonDocument("$set", new BsonDocument("IsDeleted", true))
            );
    }

    /// <summary>
    /// Spoils the stored Participation so that it cannot be read as one, as a document that something else wrote may be: its
    /// Phases are a text. Whatever reads it whole fails; a read of the members around it does not.
    /// </summary>
    public static async Task SpoilParticipationAsync(string mongoConnectionString, Guid participationId)
    {
        await Participations(mongoConnectionString)
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(participationId)),
                new BsonDocument("$set", new BsonDocument("Phases", "spoiled"))
            );
    }

    /// <summary>Spoils the stored Ranking in the same way: its Entries are a text.</summary>
    public static async Task SpoilRankingAsync(string mongoConnectionString, Guid rankingId)
    {
        await new MongoClient(mongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("event_rankings")
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(rankingId)),
                new BsonDocument("$set", new BsonDocument("Entries", "spoiled"))
            );
    }

    /// <summary>
    /// Blanks the name of the Athlete of the stored Participation: a document that the domain does not accept, which the
    /// application never writes, and that the history of the Athlete has to list all the same.
    /// </summary>
    public static async Task BlankTheAthleteNameAsync(string mongoConnectionString, Guid participationId)
    {
        await Participations(mongoConnectionString)
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(participationId)),
                new BsonDocument("$set", new BsonDocument("Combination.Athlete.Name", ""))
            );
    }

    /// <summary>
    /// Makes the stored Ranking count a Participation twice: a document that the domain does not accept, which the application
    /// never writes, and that the history of a Participant has to list all the same.
    /// </summary>
    public static async Task CountTwiceAsync(string mongoConnectionString, Guid rankingId, Guid participationId)
    {
        await new MongoClient(mongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("event_rankings")
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(rankingId)),
                new BsonDocument(
                    "$push",
                    new BsonDocument(
                        "Entries",
                        new BsonDocument
                        {
                            { "ParticipationId", EventSeed.Binary(participationId) },
                            { "IsNotRanked", false },
                            { "Rank", 1 },
                        }
                    )
                )
            );
    }

    static IMongoCollection<BsonDocument> Participations(string mongoConnectionString)
    {
        return new MongoClient(mongoConnectionString)
            .GetDatabase(UserSeed.DATABASE)
            .GetCollection<BsonDocument>("event_participations");
    }
}
