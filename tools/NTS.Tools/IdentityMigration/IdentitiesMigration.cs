using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// The dry run of <c>migrate-identities</c> (#636, ADR-0010): reads every collection that holds an Athlete or a Horse, and the Events
/// by name and date, and plans what the registry would become. It only reads. The documents are read a page at a time and dropped
/// once their copies are taken, because the Rankings of before ADR-0006 embed a whole Participation in every entry.
/// </summary>
public static class IdentitiesMigration
{
    public const string EVENT_INFORMATIONS = "event_informations";

    /// <param name="database">The database to read.</param>
    /// <param name="decisions">What the owner has decided so far.</param>
    /// <param name="pageSize">How many documents a read asks for.</param>
    /// <param name="wait">How a throttled read waits; the clock's delay unless a test gives its own.</param>
    public static async Task<IdentityPlan> Plan(
        IMongoDatabase database,
        IdentityDecisions decisions,
        int pageSize = ThrottledReads.PAGE_SIZE,
        Func<TimeSpan, Task>? wait = null
    )
    {
        var copies = new List<IdentityCopy>();
        var setups = new List<BsonDocument>();
        foreach (var name in IdentityCopies.All)
        {
            await foreach (var document in Documents(database, name, pageSize, wait))
            {
                copies.AddRange(IdentityCopies.Read(name, document));
                if (name == IdentityCopies.CONFIGURE_EVENTS)
                {
                    setups.Add(document); // few, and the report names an Event that has not started by them
                }
            }
        }

        var informations = new List<BsonDocument>();
        await foreach (var document in Documents(database, EVENT_INFORMATIONS, pageSize, wait))
        {
            informations.Add(document);
        }

        return IdentityPlan.Build(copies, IdentityEvents.Labels(informations, setups), decisions);
    }

    static IAsyncEnumerable<BsonDocument> Documents(
        IMongoDatabase database,
        string collection,
        int pageSize,
        Func<TimeSpan, Task>? wait
    )
    {
        return ThrottledReads.Documents(database.GetCollection<BsonDocument>(collection), pageSize, wait);
    }
}
