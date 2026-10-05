using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.PhaseTimes;

/// <summary>
/// ADR-0005: a Phase stores the times it received as time events and shows its Arrive, Present and Represent times from
/// them, and nothing stores a snapshot result. This turns the documents of before into that shape. Each non-empty flat time
/// of a Phase becomes an accepted event of the manual method with no recorded-at time and no actor, which is how the domain
/// tells a time that was there before events were kept; the Start and the other flags stay, the Representation request has
/// its new name, and the snapshot-results collection is dropped. It works on the documents as they are, whatever the type of
/// their ids, because it runs before the ids are converted to Guids (ADR-0009), and it is idempotent.
/// </summary>
public static class PhaseTimesMigration
{
    const string ID = "_id";
    const string COPY = "Participation";
    const string ENTRIES = "Entries";
    public const string PARTICIPATIONS = "event_participations";
    public const string RANKINGS = "event_rankings";
    public const string HANDOUTS = "event_handouts";
    public const string SNAPSHOT_RESULTS = "event-snapshotResults";

    /// <summary>
    /// Reports what it would change, and with <paramref name="apply"/> changes it, unless a flat time is not a date or a copy
    /// of a Participation is still stored: then nothing is written and the report says which.
    /// </summary>
    public static async Task<PhaseTimesReport> Run(IMongoDatabase database, bool apply)
    {
        var report = new PhaseTimesReport(apply);
        var participations = await ReadAll(database, PARTICIPATIONS);
        var changes = new List<BsonDocument>();
        var events = 0;
        foreach (var participation in participations)
        {
            if (PhaseDocuments.Migrate(participation, report.Unreadable) is not var (migrated, made))
            {
                continue;
            }

            changes.Add(migrated);
            events += made;
        }

        report.Participations = new ParticipationCounts(participations.Count, changes.Count, events);
        await FindCopies(database, report);
        await CountSnapshotResults(database, report);

        if (!apply || report.Refused)
        {
            return report;
        }

        await Persist(database, changes);
        if (report.SnapshotResultsArePresent)
        {
            await database.DropCollectionAsync(SNAPSHOT_RESULTS);
            report.SnapshotResultsDropped = true;
        }

        report.Applied = true;
        return report;
    }

    static async Task Persist(IMongoDatabase database, IReadOnlyList<BsonDocument> changes)
    {
        foreach (var document in changes)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(ID, document[ID]);
            var result = await database.GetCollection<BsonDocument>(PARTICIPATIONS).ReplaceOneAsync(filter, document);
            if (result.MatchedCount != 1)
            {
                throw new InvalidOperationException(
                    $"{PARTICIPATIONS} {PhaseTimesReport.Describe(document[ID])} was not found to replace. "
                        + "Run the migration again: what it has done stays done."
                );
            }
        }
    }

    /// <summary>A Ranking entry or a Handout that still holds the copy of a Participation, which is what <c>migrate-participation-copies</c> removes.</summary>
    static async Task FindCopies(IMongoDatabase database, PhaseTimesReport report)
    {
        foreach (var ranking in await ReadAll(database, RANKINGS))
        {
            if (ranking.TryGetValue(ENTRIES, out var entries) && entries is BsonArray array && array.Any(HoldsCopy))
            {
                report.Copies.Add(new StoredCopy(RANKINGS, ranking[ID]));
            }
        }

        foreach (var handout in await ReadAll(database, HANDOUTS))
        {
            if (HoldsCopy(handout))
            {
                report.Copies.Add(new StoredCopy(HANDOUTS, handout[ID]));
            }
        }
    }

    static bool HoldsCopy(BsonValue holder)
    {
        return holder is BsonDocument document && document.TryGetValue(COPY, out var copy) && copy is BsonDocument;
    }

    static async Task CountSnapshotResults(IMongoDatabase database, PhaseTimesReport report)
    {
        var names = await (await database.ListCollectionNamesAsync()).ToListAsync();
        report.SnapshotResultsArePresent = names.Contains(SNAPSHOT_RESULTS);
        if (report.SnapshotResultsArePresent)
        {
            report.SnapshotResults = (int)
                await database
                    .GetCollection<BsonDocument>(SNAPSHOT_RESULTS)
                    .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        }
    }

    static async Task<IReadOnlyList<BsonDocument>> ReadAll(IMongoDatabase database, string collection)
    {
        return await database
            .GetCollection<BsonDocument>(collection)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync();
    }
}
