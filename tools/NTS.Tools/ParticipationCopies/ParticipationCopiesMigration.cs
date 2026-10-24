using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.ParticipationCopies;

/// <summary>
/// ADR-0006: a Ranking entry and a Handout hold the id of a Participation and not a copy of it, and a stored
/// Participation holds none of the values the domain derives from its times. This turns the documents of before into
/// that shape and keeps the stored rank and the not-ranked mark of every entry. It works on the documents as they are,
/// whatever the type of their ids: it runs before the ids are converted to Guids (ADR-0009), and it is idempotent.
/// </summary>
public static class ParticipationCopiesMigration
{
    public const string PARTICIPATIONS = "event_participations";
    public const string RANKINGS = "event_rankings";
    public const string HANDOUTS = "event_handouts";

    /// <summary>
    /// Reports what it would change, and with <paramref name="apply"/> changes it, unless a Participation is missing:
    /// then nothing is written and the report says which.
    /// </summary>
    public static async Task<ParticipationCopiesReport> Run(IMongoDatabase database, bool apply)
    {
        var participations = await ReadAll(database, PARTICIPATIONS);
        var rankings = await ReadAll(database, RANKINGS);
        var handouts = await ReadAll(database, HANDOUTS);
        var report = new ParticipationCopiesReport(apply);
        var plan = new ParticipationCopiesPlan(report, participations);
        plan.Rankings(rankings);
        plan.Handouts(handouts);
        plan.Participations(participations);

        if (!apply || report.Refused)
        {
            return report;
        }

        await plan.Persist(database);
        report.Applied = true;
        return report;
    }

    static async Task<IReadOnlyList<BsonDocument>> ReadAll(IMongoDatabase database, string collection)
    {
        return await database
            .GetCollection<BsonDocument>(collection)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync();
    }
}
