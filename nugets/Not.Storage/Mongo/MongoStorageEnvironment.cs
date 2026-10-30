using MongoDB.Bson;
using MongoDB.Driver;
using Not.Application.Environments;

namespace Not.Storage.Mongo;

/// <summary>
/// The environment marker of a MongoDB database (#607): one document, <c>environment/environment</c>, that says which
/// environment the database is (<see cref="IStorageEnvironment"/>).
/// </summary>
public sealed class MongoStorageEnvironment : IStorageEnvironment
{
    public const string COLLECTION = "environment";
    public const string ID = "environment";

    readonly IMongoDatabase _database;

    public MongoStorageEnvironment(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var marker = await Collection().Find(new BsonDocument("_id", ID)).FirstOrDefaultAsync(cancellationToken);
        return marker != null && marker.TryGetValue("Name", out var name) && name.IsString ? name.AsString : null;
    }

    public async Task<bool> WriteAsync(string name, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var canonical =
            StorageEnvironmentNames.Canonical(name)
            ?? throw new ArgumentException(
                $"'{name}' is not an environment: {string.Join(", ", StorageEnvironmentNames.Names)}.",
                nameof(name)
            );
        var collection = Collection();
        try
        {
            var result = await collection.UpdateOneAsync(
                new BsonDocument("_id", ID),
                Builders<BsonDocument>.Update.SetOnInsert("Name", canonical).SetOnInsert("WrittenAt", at.UtcDateTime),
                new UpdateOptions { IsUpsert = true },
                cancellationToken
            );
            if (result.UpsertedId != null)
            {
                return true;
            }
        }
        catch (Exception ex)
            when (ex
                    is MongoCommandException { Code: 11000 }
                        or MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
            )
        {
            // Another write made the marker between this one's look and its insert: what it says is what counts.
        }

        var existing = await ReadAsync(cancellationToken);
        if (!string.Equals(existing, canonical, StringComparison.Ordinal))
        {
            throw new EnvironmentMarkedException(existing ?? string.Empty);
        }

        return false;
    }

    IMongoCollection<BsonDocument> Collection()
    {
        return _database.GetCollection<BsonDocument>(COLLECTION);
    }
}
