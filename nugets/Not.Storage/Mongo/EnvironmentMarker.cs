using MongoDB.Bson;
using MongoDB.Driver;

namespace Not.Storage.Mongo;

/// <summary>
/// A database was marked as another environment than the one that was asked for: a marker is not changed to another name
/// (a database that is production stays so until somebody removes the document by hand).
/// </summary>
public sealed class EnvironmentMarkedException : InvalidOperationException
{
    public EnvironmentMarkedException(string existing)
        : base($"The database is marked as '{existing}', and a marker is not changed to another name.")
    {
        Existing = existing;
    }

    /// <summary>The name the database is marked with.</summary>
    public string Existing { get; }
}

/// <summary>
/// The environment marker of a database (#607): one document, <c>environment/environment</c>, that says which environment
/// the database is. Production, staging and a developer's own database hold the same collections, so nothing else can tell
/// them apart, and the commands and routes that must never touch a production database look here first. The marker is
/// written by the commands that prepare a database and is never changed to another name.
/// </summary>
public static class EnvironmentMarker
{
    public const string COLLECTION = "environment";
    public const string ID = "environment";
    public const string PRODUCTION = "Production";
    public const string STAGING = "Staging";
    public const string DEVELOPMENT = "Development";

    /// <summary>The name of the environment a person typed, in any case, as it is written; none for anything else.</summary>
    public static string? Canonical(string? typed)
    {
        var name = typed?.Trim();
        return Names.FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether the name says production, in any case. Only that name does: a database with no name is not told to be one.</summary>
    public static bool IsProduction(string? name)
    {
        return string.Equals(name?.Trim(), PRODUCTION, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether the name says Staging or Development, in any case: the only names that let what must never touch a production
    /// database work on this one. No name does not, Production does not, and nor does a name that is none of an environment (a
    /// marker written by hand and misspelt): a database is worked on as a developer's only when it says that it is one.
    /// </summary>
    public static bool IsNonProduction(string? name)
    {
        return Canonical(name) is STAGING or DEVELOPMENT;
    }

    /// <summary>
    /// The name the database is marked with, as it is stored, or none when it has no marker. A name that is not one of an
    /// environment is told as it is: the database is not taken for another one because its marker is misspelt.
    /// </summary>
    public static async Task<string?> ReadAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var marker = await Collection(database)
            .Find(new BsonDocument("_id", ID))
            .FirstOrDefaultAsync(cancellationToken);
        return marker != null && marker.TryGetValue("Name", out var name) && name.IsString ? name.AsString : null;
    }

    /// <summary>
    /// Marks the database. True when the marker was written by this call, false when the database already says the same
    /// name, and an <see cref="EnvironmentMarkedException"/> when it says another: of several writes at once exactly one
    /// writes. The name has to be one of an environment (<see cref="Canonical"/>), and is stored as it is written there.
    /// </summary>
    public static async Task<bool> WriteAsync(
        IMongoDatabase database,
        string name,
        DateTimeOffset at,
        CancellationToken cancellationToken = default
    )
    {
        var canonical =
            Canonical(name)
            ?? throw new ArgumentException(
                $"'{name}' is not an environment: {string.Join(", ", Names)}.",
                nameof(name)
            );
        var collection = Collection(database);
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

        var existing = await ReadAsync(database, cancellationToken);
        if (!string.Equals(existing, canonical, StringComparison.Ordinal))
        {
            throw new EnvironmentMarkedException(existing ?? string.Empty);
        }

        return false;
    }

    /// <summary>The environments a database can be, as they are written.</summary>
    public static IReadOnlyList<string> Names { get; } = [PRODUCTION, STAGING, DEVELOPMENT];

    static IMongoCollection<BsonDocument> Collection(IMongoDatabase database)
    {
        return database.GetCollection<BsonDocument>(COLLECTION);
    }
}
