using MongoDB.Bson;
using MongoDB.Driver;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// Keeps the assignments of a run in the database while it lasts (#637). They are made once, from the data before the first write,
/// because a run that stopped has converted some copies, and the copies no longer say what the matching needs (their integer id is
/// the Guid now). A run that finds them finishes with them, and the last step deletes them. They hold the names of real people, as
/// the data does, and are gone with the run.
/// </summary>
internal sealed class IdentityPlanStore
{
    const string ID = "plan";
    public const string COLLECTION = "migrate_identities_plan";

    readonly IMongoDatabase _database;
    readonly Func<TimeSpan, Task>? _wait;

    public IdentityPlanStore(IMongoDatabase database, Func<TimeSpan, Task>? wait)
    {
        _database = database;
        _wait = wait;
    }

    /// <returns>Whether a run that stopped has kept its assignments.</returns>
    public async Task<bool> ExistsAsync()
    {
        return await ThrottledReads.Retried(() => Documents().Find(new BsonDocument("_id", ID)).AnyAsync(), _wait);
    }

    /// <returns>The assignments a run that stopped made, none if there is no such run.</returns>
    public async Task<IdentityAssignments?> LoadAsync()
    {
        var found = await ThrottledReads.Retried(
            () => Documents().Find(new BsonDocument("_id", ID)).FirstOrDefaultAsync(),
            _wait
        );
        return found == null ? null : IdentityAssignments.FromDocument(found);
    }

    public async Task SaveAsync(BsonDocument assignments)
    {
        var document = new BsonDocument("_id", ID);
        document.AddRange(assignments);
        await ThrottledReads.Retried(
            () =>
                Documents()
                    .ReplaceOneAsync(new BsonDocument("_id", ID), document, new ReplaceOptions { IsUpsert = true }),
            _wait
        );
    }

    public async Task DeleteAsync()
    {
        await ThrottledReads.Retried(() => _database.DropCollectionAsync(COLLECTION), _wait);
    }

    IMongoCollection<BsonDocument> Documents()
    {
        return _database.GetCollection<BsonDocument>(COLLECTION);
    }
}
