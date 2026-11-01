using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Not.Exceptions;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;
using NTS.Tools.Shared;

namespace NTS.Tools.IdentityMigration;

/// <summary>Where the placings of a Ranking are (#637, #640).</summary>
public enum PlacingsState
{
    /// <summary>No entry holds a rank: the one state that finalising writes to.</summary>
    Unplaced = 1,

    /// <summary>Some entries hold a rank and some do not, which nothing produces: it is told and left alone.</summary>
    Partial = 2,

    /// <summary>Every entry holds a rank, or there is no entry.</summary>
    Final = 3,
}

/// <summary>A Ranking of an Event that has ended, and where its placings are (#637).</summary>
public sealed class HistoricRanking
{
    public HistoricRanking(string id, string eventId, string? name, PlacingsState state)
    {
        Id = id;
        EventId = eventId;
        Name = name;
        State = state;
    }

    /// <summary>The id of the Ranking, as the reports tell it.</summary>
    public string Id { get; }

    public string EventId { get; }
    public string? Name { get; }
    public PlacingsState State { get; }
}

/// <summary>
/// The Rankings of the Events that have ended, and their final placings (#637, ADR-0006, #640). <see cref="Scan"/> finds them in the
/// documents as they are stored, whatever their ids, to list what an apply would finalise. <see cref="FinaliseAsync"/> stores the
/// placings with the domain operation of the Results (<see cref="Ranking.Finalise"/>), which reads only Guid-keyed documents, so it
/// runs after the conversion. It writes a Ranking only while no entry of it holds a rank, as the host's sweep does, so that a second
/// run, or the sweep, changes nothing; a rank that is stored is never rewritten.
/// </summary>
public static class HistoricRankings
{
    const string EVENT_INFORMATIONS = "event_informations";
    const string RANKINGS = "event_rankings";
    const string PARTICIPATIONS = "event_participations";

    /// <summary>The Rankings of the Events that have ended, found in the stored documents.</summary>
    public static async Task<IReadOnlyList<HistoricRanking>> Scan(
        IMongoDatabase database,
        DateTimeOffset now,
        int pageSize = ThrottledReads.PAGE_SIZE,
        Func<TimeSpan, Task>? wait = null
    )
    {
        var historic = new HashSet<string>(StringComparer.Ordinal);
        await foreach (
            var information in ThrottledReads.Documents(
                database.GetCollection<BsonDocument>(EVENT_INFORMATIONS),
                pageSize,
                wait
            )
        )
        {
            if (IsHistoric(information, now))
            {
                historic.Add(IdentityCopies.Describe(information["_id"]));
            }
        }

        var found = new List<HistoricRanking>();
        await foreach (
            var ranking in ThrottledReads.Documents(database.GetCollection<BsonDocument>(RANKINGS), pageSize, wait)
        )
        {
            if (
                !IsDeleted(ranking)
                && ranking.TryGetValue("EventId", out var eventId)
                && historic.Contains(IdentityCopies.Describe(eventId))
            )
            {
                found.Add(
                    new HistoricRanking(
                        IdentityCopies.Describe(ranking["_id"]),
                        IdentityCopies.Describe(eventId),
                        Records.TextOf(ranking, "Name"),
                        StateOf(ranking)
                    )
                );
            }
        }

        return found;
    }

    /// <summary>
    /// Stores the placings of every Ranking of an Event that has ended and has none, by the Results code, over the Participations of
    /// the Event and its rules. A Ranking the domain refuses is left as it is and told, and the others are done.
    /// </summary>
    public static async Task<RankingsReport> FinaliseAsync(
        IMongoDatabase database,
        DateTimeOffset now,
        int pageSize = ThrottledReads.PAGE_SIZE,
        Func<TimeSpan, Task>? wait = null,
        Func<Guid, Task>? beforeWritten = null
    )
    {
        ModelMapping.Configure();
        var report = new RankingsReport();
        var rankings = database.GetCollection<BsonDocument>(RANKINGS);
        await foreach (
            var information in ThrottledReads.Documents(
                database.GetCollection<BsonDocument>(EVENT_INFORMATIONS),
                pageSize,
                wait
            )
        )
        {
            if (!IsHistoric(information, now) || Records.GuidOf(information, "_id") is not { } eventId)
            {
                continue;
            }

            var ofTheEvent = await ThrottledReads.Retried(
                () => rankings.Find(new BsonDocument("EventId", Records.Uuid(eventId))).ToListAsync(),
                wait
            );
            var unplaced = new List<BsonDocument>();
            foreach (
                var ranking in ofTheEvent
                    .Where(x => !IsDeleted(x))
                    .OrderBy(x => IdentityCopies.Describe(x["_id"]), StringComparer.Ordinal)
            )
            {
                var id = Records.GuidOf(ranking, "_id");
                switch (StateOf(ranking))
                {
                    case PlacingsState.Final when id is { } final:
                        report.AlreadyFinal.Add(final);
                        break;
                    case PlacingsState.Partial when id is { } partial:
                        report.SomePlacingsStored.Add(partial);
                        break;
                    case PlacingsState.Unplaced:
                        unplaced.Add(ranking);
                        break;
                }
            }

            if (unplaced.Count > 0)
            {
                await FinaliseAsync(database, information, eventId, unplaced, report, wait, beforeWritten);
            }
        }

        return report;
    }

    internal static PlacingsState StateOf(BsonDocument ranking)
    {
        var entries =
            ranking.TryGetValue("Entries", out var value) && value is BsonArray array
                ? array.OfType<BsonDocument>().ToList()
                : [];
        var placed = entries.Count(x => x.TryGetValue("Rank", out var rank) && !rank.IsBsonNull);
        return placed == entries.Count ? PlacingsState.Final
            : placed == 0 ? PlacingsState.Unplaced
            : PlacingsState.Partial;
    }

    /// <summary>Whether the Event has ended at the instant: the rule of the Api (an Event is Live until the end of its last day).</summary>
    internal static bool IsHistoric(BsonDocument information, DateTimeOffset now)
    {
        return !IsDeleted(information)
            && information.TryGetValue("EndDay", out var end)
            && end is BsonDateTime date
            && EventStageRule.Of(started: true, new DateTimeOffset(date.ToUniversalTime()), now) == EventStage.Historic;
    }

    internal static bool IsDeleted(BsonDocument document)
    {
        return document.TryGetValue("IsDeleted", out var deleted) && deleted.IsBoolean && deleted.AsBoolean;
    }

    static async Task FinaliseAsync(
        IMongoDatabase database,
        BsonDocument information,
        Guid eventId,
        List<BsonDocument> unplaced,
        RankingsReport report,
        Func<TimeSpan, Task>? wait,
        Func<Guid, Task>? beforeWritten
    )
    {
        var participations = await ParticipationsAsync(database, eventId, wait);
        RegionalRules? rules;
        try
        {
            rules = RulesOf(information);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            // rules that cannot be read leave the Results of the Event uncomposed: its Rankings are told, like the ones the domain refuses
            report.Refused.AddRange(unplaced.Select(x => Records.GuidOf(x, "_id")).OfType<Guid>());
            return;
        }

        var rankings = database.GetCollection<BsonDocument>(RANKINGS);
        foreach (var stored in unplaced)
        {
            var id = Records.GuidOf(stored, "_id");
            if (id is not { } rankingId)
            {
                continue;
            }

            RankingFinalisation finalisation;
            try
            {
                finalisation = BsonSerializer
                    .Deserialize<RankingModel>(stored)
                    .MapToEntity()
                    .Finalise(participations, rules, EventStage.Historic);
            }
            catch (Exception ex) when (ex is GuardException or ValidationException or FormatException)
            {
                // a Ranking that is not one, or whose Results cannot be composed, has no placings to keep, for the host as for a reader
                report.Refused.Add(rankingId);
                continue;
            }

            if (finalisation.Outcome != RankingFinalisationOutcome.Finalised)
            {
                continue;
            }

            var placed = new BsonArray(RankingModel.From(finalisation.Ranking).Entries.Select(x => x.ToBsonDocument()));
            if (beforeWritten != null)
            {
                await beforeWritten(rankingId);
            }

            var written = await ThrottledReads.Retried(
                () =>
                    rankings.UpdateOneAsync(
                        new BsonDocument
                        {
                            { "_id", Records.Uuid(rankingId) },
                            {
                                "Entries",
                                BsonDocument.Parse("""{ "$not": { "$elemMatch": { "Rank": { "$ne": null } } } }""")
                            },
                        },
                        new BsonDocument("$set", new BsonDocument("Entries", placed))
                    ),
                wait
            );
            if (written.ModifiedCount > 0)
            {
                report.Finalised.Add(rankingId);
            }
        }
    }

    /// <summary>The Participations of the Event as the domain reads them; one that cannot be read is left out, and a Ranking that counts it is refused.</summary>
    static async Task<List<Participation>> ParticipationsAsync(
        IMongoDatabase database,
        Guid eventId,
        Func<TimeSpan, Task>? wait
    )
    {
        var documents = await ThrottledReads.Retried(
            () =>
                database
                    .GetCollection<BsonDocument>(PARTICIPATIONS)
                    .Find(new BsonDocument("EventId", Records.Uuid(eventId)))
                    .ToListAsync(),
            wait
        );
        var participations = new List<Participation>();
        foreach (var document in documents.Where(x => !IsDeleted(x)))
        {
            try
            {
                participations.Add(BsonSerializer.Deserialize<ParticipationModel>(document).MapToEntity());
            }
            catch (Exception ex) when (ex is GuardException or ValidationException or FormatException)
            {
                // the Rankings that count it are refused, each as the domain says
            }
        }

        return participations;
    }

    static RegionalRules? RulesOf(BsonDocument information)
    {
        return information.TryGetValue("RegionalRules", out var rules) && rules is BsonDocument document
            ? BsonSerializer.Deserialize<RegionalRulesModel>(document).MapToEntity()
            : null;
    }
}
